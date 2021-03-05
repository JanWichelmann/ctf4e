using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ctf4e.Api.DependencyInjection;
using Ctf4e.Api.Options;
using Ctf4e.LessonServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ctf4e.LessonServer.Constants;
using Ctf4e.LessonServer.Options;
using Ctf4e.LessonServer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nito.AsyncEx;

namespace Ctf4e.LessonServer
{
    public class Startup
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private LessonOptions _lessonOptions;

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions<CtfApiOptions>().Bind(_configuration.GetSection(nameof(CtfApiOptions)));
            _lessonOptions = _configuration.GetSection(nameof(LessonOptions)).Get<LessonOptions>();
            services.AddOptions<LessonOptions>().Bind(_configuration.GetSection(nameof(LessonOptions)));

            // CTF API services
            services.AddCtfApiCryptoService();
            services.AddCtfApiClient();

            // Memory cache
            services.AddMemoryCache();
            
            // Lesson configuration
            services.AddSingleton<ILessonConfigurationService>(s =>
            {
                var lessonConfig = new LessonConfigurationService(s.GetRequiredService<IOptionsMonitor<LessonOptions>>());
                lessonConfig.ReadConfigurationAsync().GetAwaiter().GetResult();
                return lessonConfig;
            });

            // Lesson state service
            services.AddSingleton<IStateService, StateService>();
            
            // Docker container support
            services.AddSingleton<IDockerService, DockerService>();

            // Change name of antiforgery cookie
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "ctf4e-lessonserver.antiforgery";
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            // Authentication
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.Cookie.Name = "ctf4e-lessonserver.session";
                options.LoginPath = "/auth";
                options.LogoutPath = "/auth/logout";
                options.AccessDeniedPath = "/auth";
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthenticationStrings.PolicyAdminMode, policy => policy.RequireClaim(AuthenticationStrings.ClaimAdminMode, true.ToString()));
            });

            // Use MVC
            var mvcBuilder = services.AddControllersWithViews(options =>
            {
            });

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
            if(_lessonOptions.ProxySupport)
            {
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    RequireHeaderSymmetry = true,
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
                    KnownNetworks = { new IPNetwork(IPAddress.Parse(_lessonOptions.ProxyNetworkAddress), _lessonOptions.ProxyNetworkPrefix) }
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
            
            // Verbose stack traces
            if(_lessonOptions.DevelopmentMode)
                app.UseDeveloperExceptionPage();
            
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
}
