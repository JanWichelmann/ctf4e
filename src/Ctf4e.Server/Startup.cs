using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AutoMapper;
using Ctf4e.Server.Constants;
using Ctf4e.Server.Data;
using Ctf4e.Server.Options;
using Ctf4e.Server.Services;
using Ctf4e.Server.Services.Sync;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
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

namespace Ctf4e.Server
{
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

            // Moodle connection
            services.AddHttpClient();
            services.AddMoodleLtiApi();
            services.AddMoodleGradebook();

            // Database
            services.AddDbContextPool<CtfDbContext>(options =>
            {
                options.UseMySql($"Server={dbOptions.Server};Database={dbOptions.Database};User={dbOptions.User};Password={dbOptions.Password};", _ =>
                {
                });
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

            // Change name of antiforgery cookie
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "ctf4e.antiforgery";
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            // Authentication
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.Cookie.Name = "ctf4e.session";
                options.LoginPath = "/auth";
                options.LogoutPath = "/auth/logout";
                options.AccessDeniedPath = "/auth";
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthenticationStrings.PolicyIsGroupMember, policy => policy.RequireClaim(AuthenticationStrings.ClaimIsGroupMember, true.ToString()));
                options.AddPolicy(AuthenticationStrings.PolicyIsAdmin, policy => policy.RequireClaim(AuthenticationStrings.ClaimIsAdmin, true.ToString()));
                options.AddPolicy(AuthenticationStrings.PolicyIsPrivileged, policy => policy.RequireAssertion(context =>
                {
                    return context.User.Claims.Any(c => c.Type == AuthenticationStrings.ClaimIsAdmin && c.Value == true.ToString())
                           || context.User.Claims.Any(c => c.Type == AuthenticationStrings.ClaimIsTutor && c.Value == true.ToString());
                }));
            });

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

                    // Only allow admins access to profiler results
                    static bool AuthorizeFunc(HttpRequest request)
                    {
                        return request.HttpContext.User.Claims.Any(c => c.Type == AuthenticationStrings.ClaimIsAdmin && c.Value == true.ToString());
                    }

                    options.ResultsAuthorize = AuthorizeFunc;
                    options.ResultsListAuthorize = AuthorizeFunc;

                    // Reduce noise for database queries
                    options.TrackConnectionOpenClose = false;

                    // Restrict profiled paths
                    options.ShouldProfile = request =>
                    {
                        // Do not profile static files
                        if(request.Path.Value == null)
                            return true;
                        if(request.Path.Value.StartsWith("/lib")
                           || request.Path.Value.StartsWith("/css"))
                            return false;

                        return true;
                    };
                }).AddEntityFramework();
            }

            // IDE-only tools
            if(_environment.IsDevelopment())
            {
                mvcBuilder.AddRazorRuntimeCompilation();
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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

            // Localization
            // We keep the default cookie name, so the setting automatically translates to potential other server under the current domain
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
            
            // Verbose stack traces
            if(_mainOptions.DevelopmentMode)
                app.UseDeveloperExceptionPage();
            
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

            // Install MiniProfiler
            //     Needs to be after UseAuthorization(), else authorization doesn't work.
            //     However, this does mean that earlier middleware isn't profiled!
            //     If this ever becomes an issue, remove authorization or use a workaround.
            //     Relevant: https://stackoverflow.com/questions/59290361/what-is-an-appropriate-way-to-drive-miniprofiler-nets-resultsauthorize-handler
            if(_mainOptions.DevelopmentMode)
                app.UseMiniProfiler();

            // Create routing endpoints
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}