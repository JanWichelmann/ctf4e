using System;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                new Microsoft.Extensions.Logging.Debug.DebugLoggerProvider()
            });

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            services.AddOptions<MoodleLti.Options.MoodleLtiOptions>().Bind(_configuration.GetSection(nameof(MoodleLti.Options.MoodleLtiOptions)));
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
                options.UseMySql($"Server={dbOptions.Server};Database={dbOptions.Database};User={dbOptions.User};Password={dbOptions.Password};", mysqlOptions => { });
                if(_environment.IsDevelopment())
                {
                    options.UseLoggerFactory(_debugLoggerFactory)
                        .EnableSensitiveDataLogging();
                }
            });

            // Entity/Model mapping
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // Model/database services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISlotService, SlotService>();
            services.AddScoped<ILabService, LabService>();
            services.AddScoped<IExerciseService, ExerciseService>();
            services.AddScoped<IFlagService, FlagService>();
            services.AddScoped<ILabExecutionService, LabExecutionService>();
            services.AddScoped<IScoreboardService, ScoreboardService>();

            // Services for configuration and utility functions
            services.AddScoped<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IFlagPointService, FlagPointService>();
            services.AddSingleton<IScoreboardCacheService, ScoreboardCacheService>();
            services.AddScoped<IMoodleService, MoodleService>();
            services.AddScoped<ICsvService, CsvService>();

            // Change name of antiforgery cookie
            services.AddAntiforgery(options => { options.Cookie.Name = "ctf4e.antiforgery"; });

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
            var mvcBuilder = services.AddControllersWithViews(options => { options.SuppressAsyncSuffixInActionNames = false; });

            // Development tools
            if(_mainOptions.DevelopmentMode)
            {
                // Add MiniProfiler
                services.AddMiniProfiler(options =>
                {
                    // Put profiler results to dev route
                    options.RouteBasePath = "/dev/profiler";

                    // Only allow admins access to profiler results
                    // TODO broken
                    /*
                    static bool AuthorizeFunc(HttpRequest request)
                    {
                        return request.HttpContext.User.Claims.Any(c => c.Type == AuthenticationStrings.ClaimIsAdmin && c.Value == true.ToString());
                    }
                    options.ResultsAuthorize = AuthorizeFunc;
                    options.ResultsListAuthorize = AuthorizeFunc;
                    */
                    
                    // Reduce noise for database queries
                    options.TrackConnectionOpenClose = false;

                    // Restrict profiled paths
                    options.ShouldProfile = request =>
                    {
                        // Do not profile static files
                        if(request.Path.Value.StartsWith("/lib")
                           || request.Path.Value.StartsWith("/css"))
                            return false;

                        return true;
                    };
                }).AddEntityFramework();

                // IDE-only tools
                if(_environment.IsDevelopment())
                {
                    mvcBuilder.AddRazorRuntimeCompilation();
                }
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Development tools
            if(_mainOptions.DevelopmentMode)
            {
                // Verbose stack traces
                app.UseDeveloperExceptionPage();

                // MiniProfiler
                app.UseMiniProfiler();
            }

            // Serve static files from wwwroot/
            app.UseStaticFiles();

            // Enable routing
            app.UseRouting();

            // Authentication
            app.UseCookiePolicy(new CookiePolicyOptions
            {
                Secure = CookieSecurePolicy.SameAsRequest,
                MinimumSameSitePolicy = SameSiteMode.Strict
            });
            app.UseAuthentication();
            app.UseAuthorization();

            // Create routing endpoints
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}