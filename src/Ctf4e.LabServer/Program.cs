using System.Collections.Generic;
using System.Linq;
using System.Net;
using Ctf4e.Api.DependencyInjection;
using Ctf4e.Api.Options;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.Options;
using Ctf4e.LabServer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);
{
    // Register builder.Services

    // Configuration
    var configurationSection = builder.Configuration.GetSection("Ctf4e");
    builder.Services.AddOptions<LabOptions>().Bind(configurationSection.GetSection(nameof(LabOptions)));
    builder.Services.AddOptions<CtfApiOptions>().Bind(configurationSection.GetSection(nameof(CtfApiOptions)));

    // CTF API builder.Services
    builder.Services.AddCtfApiCryptoService();
    builder.Services.AddCtfApiClient();

    // Localization
    builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

    // Memory cache
    builder.Services.AddMemoryCache();

    // Lab configuration
    builder.Services.AddSingleton<ILabConfigurationService>(s =>
    {
        var labConfig = new LabConfigurationService(s.GetRequiredService<IOptionsMonitor<LabOptions>>());
        labConfig.ReadConfigurationAsync().GetAwaiter().GetResult();
        return labConfig;
    });

    // Lab state service
    builder.Services.AddSingleton<IStateService, StateService>();

    // Docker container support
    builder.Services.AddSingleton<IDockerService, DockerService>();

    // Change name of antiforgery cookie
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "ctf4e-labserver.antiforgery";
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    // Authentication
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
    {
        options.Cookie.Name = "ctf4e-labserver.session";
        options.LoginPath = "/auth";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth";
    });
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthenticationStrings.PolicyAdminMode, policy => policy.RequireClaim(AuthenticationStrings.ClaimAdminMode, true.ToString()));
    });

    // Use MVC
    var mvcBuilder = builder.Services.AddControllersWithViews(_ =>
    {
    }).AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix);

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

    var labOptions = app.Services.GetRequiredService<IOptions<LabOptions>>().Value;

    // Verbose stack traces
    if(app.Environment.IsDevelopment())
        app.UseDeveloperExceptionPage(new DeveloperExceptionPageOptions { SourceCodeLineCount = 3 });

    // Forward headers when used behind a proxy
    // Must be the very first middleware
    if(labOptions.ProxySupport)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            RequireHeaderSymmetry = true,
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
            KnownNetworks = { new IPNetwork(IPAddress.Parse(labOptions.ProxyNetworkAddress), labOptions.ProxyNetworkPrefix) }
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

    // Localization
    // We keep the default cookie name, so the setting automatically translates to/from potential other server under the current domain
    var supportedCultures = new[] { "en-US", "de-DE" };
    string defaultCulture = supportedCultures.Contains(labOptions.DefaultCulture) ? labOptions.DefaultCulture : supportedCultures[0];
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

    // Simple status code pages
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

await app.RunAsync();