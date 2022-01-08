using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ctf4e.Server.Authorization;
using Ctf4e.Server.Data;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Extensions.Primitives;
using MoodleLti.Options;
using AuthenticationStrings = Ctf4e.Server.Authorization.AuthenticationStrings;

namespace Ctf4e.Server;

public class Startup
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private MainOptions _mainOptions;

    private readonly LoggerFactory _debugLoggerFactory =
        new LoggerFactory(new[]
        {
            new DebugLoggerProvider()
        });

    public Startup(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.AddOptions<MoodleLtiOptions>().Bind(_configuration.GetSection(nameof(MoodleLtiOptions)));
        _mainOptions = _configuration.GetSection(nameof(MainOptions)).Get<MainOptions>();
        services.AddOptions<MainOptions>().Bind(_configuration.GetSection(nameof(MainOptions)));
        var dbOptions = _configuration.GetSection(nameof(CtfDbOptions)).Get<CtfDbOptions>();

        // Key persistence
        if(_mainOptions.SecretsDirectory != null)
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(_mainOptions.SecretsDirectory));
        }

        // Moodle connection
        services.AddHttpClient();
        services.AddMoodleLtiApi();
        services.AddMoodleGradebook();

        // Database
        services.AddDbContextPool<CtfDbContext>(options =>
        {
            options.UseMySql($"Server={dbOptions.Server};Database={dbOptions.Database};User={dbOptions.User};Password={dbOptions.Password};",
                ServerVersion.Parse("10.5.8-mariadb"), mysqlOptions => mysqlOptions.EnableRetryOnFailure(3));
            if(_environment.IsDevelopment())
            {
                options.UseLoggerFactory(_debugLoggerFactory)
                    .EnableSensitiveDataLogging();
            }
        });

        // Localization
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Entity/Model mapping
        services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        // Memory cache
        services.AddMemoryCache();

        // Model/database services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISlotService, SlotService>();
        services.AddScoped<ILabService, LabService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IFlagService, FlagService>();
        services.AddScoped<ILabExecutionService, LabExecutionService>();
        services.AddScoped<IScoreboardService, ScoreboardService>();

        // Markdown parser
        services.AddSingleton<IMarkdownService, MarkdownService>();

        // Configuration service
        services.AddScoped<IConfigurationService, ConfigurationService>();

        // Export/sync services
        services.AddScoped<IMoodleService, MoodleService>();
        services.AddScoped<ICsvService, CsvService>();
        services.AddScoped<IDumpService, DumpService>();

        // Forward headers when used behind a proxy
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        });

        // Change name of antiforgery cookie
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "ctf4e.antiforgery";
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        // Authentication
        services.AddAuthentication(options =>
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
        services.AddSingleton<IAuthorizationHandler, UserPrivilegeHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, UserPrivilegePolicyProvider>();

        // Use MVC
        var mvcBuilder = services.AddControllersWithViews(_ =>
        {
        }).AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix);

        // Development tools
        if(_mainOptions.DevelopmentMode)
        {
            // Add MiniProfiler
            services.AddMiniProfiler(options =>
            {
                // Put profiler results to dev route
                options.RouteBasePath = "/dev/profiler";

                // Only allow access to profiler results in developer mode
                static async Task<bool> AuthorizeAsync(HttpRequest request)
                {
                    // Get required services
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
                    var user = await userService.FindByIdAsync(userId, context.RequestAborted);

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
        if(_environment.IsDevelopment())
        {
            mvcBuilder.AddRazorRuntimeCompilation();
        }
#endif
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Verbose stack traces
        if(_environment.IsDevelopment())
            app.UseDeveloperExceptionPage();
        
        // Forward headers when used behind a proxy
        // Must be the very first middleware
        if(_mainOptions.ProxySupport)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                RequireHeaderSymmetry = true,
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
                KnownNetworks = { new IPNetwork(IPAddress.Parse(_mainOptions.ProxyNetworkAddress), _mainOptions.ProxyNetworkPrefix) }
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
        string defaultCulture = supportedCultures.Contains(_mainOptions.DefaultCulture) ? _mainOptions.DefaultCulture : supportedCultures[0];
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
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}