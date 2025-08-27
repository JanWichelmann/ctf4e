using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Data;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using LtiAdvantageTools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using MoodleLti.Options;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);
{
    // Register services
    
    // Configuration
    var configurationSection = builder.Configuration.GetSection("Ctf4e");
    var mainOptions = configurationSection.GetSection(nameof(MainOptions)).Get<MainOptions>() ?? throw new Exception("Could not find main configuration.");
    builder.Services.AddOptions<MainOptions>().Bind(configurationSection.GetSection(nameof(MainOptions)));

    // Secret storage
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<CtfDbContext>();

    // LTI/Moodle connection
    builder.Services.AddHttpClient();
    builder.Services.AddMoodleLtiApi();
    builder.Services.AddMoodleGradebook();
    builder.Services.AddOptions<MoodleLtiOptions>().Bind(configurationSection.GetSection(nameof(MoodleLtiOptions)));
    builder.Services.AddScoped<ILtiLoginService, LtiLoginService>();
    builder.Services.AddScoped<ILtiConfigurationStore, LtiConfigurationStore>();
    builder.Services.AddOptions<LtiAdvantageOptions>().Bind(configurationSection.GetSection("Lti"));

    // Database
    var dbOptions = configurationSection.GetSection(nameof(CtfDbOptions)).Get<CtfDbOptions>() ?? throw new Exception("Could not find database configuration.");
    builder.Services.AddDbContextPool<CtfDbContext>(options =>
    {
        options.UseMySql(
            $"Server={dbOptions.Server};Database={dbOptions.Database};User={dbOptions.User};Password={dbOptions.Password};",
            ServerVersion.Parse(dbOptions.ServerVersion),
            mysqlOptions => mysqlOptions.EnableRetryOnFailure(3)
        );
        
        if(builder.Environment.IsDevelopment())
            options.EnableSensitiveDataLogging().EnableDetailedErrors();
    });

    // Entity/Model mapping
    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
    
    // Localization
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    // Memory cache
    builder.Services.AddMemoryCache();

    // Model/database service
    builder.Services.AddScoped<GenericCrudService<CtfDbContext>>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IGroupService, GroupService>();
    builder.Services.AddScoped<ISlotService, SlotService>();
    builder.Services.AddScoped<ILabService, LabService>();
    builder.Services.AddScoped<IExerciseService, ExerciseService>();
    builder.Services.AddScoped<IFlagService, FlagService>();
    builder.Services.AddScoped<ILabExecutionService, LabExecutionService>();
    
    // Scoreboard computations
    builder.Services.AddSingleton<ScoreboardUtilities>();
    builder.Services.AddScoped<IScoreboardService, ScoreboardService>();
    builder.Services.AddScoped<IAdminScoreboardService, AdminScoreboardService>();

    // Markdown parser
    builder.Services.AddSingleton<IMarkdownService, MarkdownService>();

    // Configuration service
    builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

    // Export/sync services
    builder.Services.AddScoped<IMoodleService, MoodleService>();
    builder.Services.AddScoped<ICsvService, CsvService>();
    builder.Services.AddScoped<IDumpService, DumpService>();

    // Rate limiting
    builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();

    // Forward headers when used behind a proxy
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    });

    // Change name of antiforgery cookie
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "ctf4e.antiforgery";
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // Authentication
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }).AddCookie(options =>
    {
        options.Cookie.Name = "ctf4e.session";
        options.LoginPath = "/auth";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth";
    });

    // Authorization
    builder.Services.AddSingleton<IAuthorizationHandler, UserPrivilegeHandler>();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, UserPrivilegePolicyProvider>();

    // Use MVC
    var mvcBuilder = builder.Services.AddControllersWithViews(_ =>
    {
    }).AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix);

    // Development tools
    if(mainOptions.DevelopmentMode)
    {
        // Add MiniProfiler
        builder.Services.AddMiniProfiler(options =>
        {
            // Put profiler results to dev route
            options.RouteBasePath = "/dev/profiler";

            // Only allow access to profiler results in developer mode
            static async Task<bool> AuthorizeAsync(HttpRequest request)
            {
                // Get required builder.Services
                var context = request.HttpContext;
                var userService = context.RequestServices.GetRequiredService<IUserService>();

                // Check whether user is authenticated
                var authenticateResult = await context.AuthenticateAsync();
                if(!authenticateResult.Succeeded)
                    return false;

                // Retrieve user data
                var userIdClaim = authenticateResult.Ticket.Principal.Claims.FirstOrDefault(c => c.Type == AuthenticationStrings.ClaimUserId);
                if(userIdClaim == null)
                    return false;
                int userId = int.Parse(userIdClaim.Value);
                var user = await userService.FindUserByIdAsync(userId, context.RequestAborted);

                // If the user does not exist, deny all access
                if(user == null)
                    return false;

                // Check whether the user has the necessary privileges
                return user.Privileges.HasPrivileges(UserPrivileges.Admin);
            }

            options.ResultsAuthorizeAsync = AuthorizeAsync;
            options.ResultsListAuthorizeAsync = AuthorizeAsync;

            // Reduce noise for database queries
            options.TrackConnectionOpenClose = false;

            // Restrict profiled paths
            options.ShouldProfile = request =>
            {
                // Do not profile static files
                if(request.Path.Value.StartsWith("/lib")
                   || request.Path.Value.StartsWith("/css")
                   || request.Path.Value.StartsWith("/img"))
                    return false;

                return true;
            };
        }).AddEntityFramework();
    }

#if DEBUG
    // IDE-only tools
    if(builder.Environment.IsDevelopment())
    {
        mvcBuilder.AddRazorRuntimeCompilation();
    }
#endif
}

var app = builder.Build();
{
    // Build HTTP request pipeline

    var mainOptions = app.Services.GetRequiredService<IOptions<MainOptions>>().Value;
    
    // Verbose stack traces
    if(app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage(new DeveloperExceptionPageOptions{SourceCodeLineCount = 3});

    // Forward headers when used behind a proxy
    // Must be the very first middleware
    if(mainOptions.ProxySupport)
    {
        var proxyNetworkIp = IPAddress.Parse(mainOptions.ProxyNetworkAddress);
        var proxyNetwork = new IPNetwork(proxyNetworkIp, mainOptions.ProxyNetworkPrefix);
        
        // Help debugging proxy issues
        app.Use((context, next) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress!;
            if(!proxyNetworkIp.IsIPv4MappedToIPv6 && remoteIp.IsIPv4MappedToIPv6)
                remoteIp = remoteIp.MapToIPv4();
            if(!proxyNetwork.Contains(remoteIp))
            { 
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                context.Response.WriteAsync("Bad gateway. Check log for more details.");
                logger.LogError("Request from {RemoteIp} was blocked by proxy settings (proxy network: {ProxyNetworkIp}/{ProxyNetworkPrefix}).", remoteIp, proxyNetwork.Prefix, proxyNetwork.PrefixLength);    
                return Task.CompletedTask;
            }
            
            return next();
        });
        
        // Forwarding
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            RequireHeaderSymmetry = true,
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
            KnownNetworks = { proxyNetwork },
            ForwardLimit = 1
        });
        app.Use((context, next) =>
        {
            if(context.Request.Headers.TryGetValue("X-Forwarded-Prefix", out var forwardedPrefix))
            {
                if(!StringValues.IsNullOrEmpty(forwardedPrefix))
                    context.Request.PathBase = PathString.FromUriComponent(forwardedPrefix.ToString());
            }

            return next();
        });
    }

    // Install MiniProfiler
    // This can be very early in the pipeline, as we use a custom authorization function which does not rely on the framework-supplied one
    app.UseMiniProfiler();

    // Localization
    // We keep the default cookie name, so the setting automatically translates to potential other servers under the current domain
    var supportedCultures = new[] { "en-US", "de-DE" };
    string defaultCulture = supportedCultures.Contains(mainOptions.DefaultCulture) ? mainOptions.DefaultCulture : supportedCultures[0];
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture(defaultCulture)
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
    app.UseRequestLocalization(localizationOptions);

    // Show simple status code pages in case of errors
    app.UseStatusCodePages();

    // Serve static files from wwwroot/
    app.UseStaticFiles();

    // Enable routing
    app.UseRouting();

    // Authentication
    app.UseCookiePolicy(new CookiePolicyOptions
    {
        Secure = CookieSecurePolicy.SameAsRequest,
        MinimumSameSitePolicy = SameSiteMode.Lax
    });
    app.UseAuthentication();
    app.UseAuthorization();

    // Create routing endpoints
    app.MapControllers();
}

// Ensure all services are initialized and ready
using(var scope = app.Services.CreateScope())
{
    // Run migrations
    // Usually this should not be done by the webserver, but manually before deploying the new version.
    // However, since this app is not yet compatible to a load balancing environment, we leave this for now.
    var dbContext = scope.ServiceProvider.GetRequiredService<CtfDbContext>();
    if((await dbContext.Database.GetPendingMigrationsAsync()).Any())
        await dbContext.Database.MigrateAsync();
    
    // Initialize singleton services
    var scoreboardUtilities = app.Services.GetRequiredService<ScoreboardUtilities>();
    await scoreboardUtilities.InitFlagPointParametersAsync(app.Services.GetRequiredService<IServiceScopeFactory>(), CancellationToken.None);
}

await app.RunAsync();