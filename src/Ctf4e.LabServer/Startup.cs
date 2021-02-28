using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ctf4e.Api.DependencyInjection;
using Ctf4e.Api.Options;
using Ctf4e.LabServer.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ctf4e.LabServer.Constants;
using Ctf4e.LabServer.Services;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace Ctf4e.LabServer
{
    public class Startup
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private LabOptions _labOptions;

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Options
            services.AddOptions<CtfApiOptions>().Bind(_configuration.GetSection(nameof(CtfApiOptions)));
            _labOptions = _configuration.GetSection(nameof(LabOptions)).Get<LabOptions>();
            services.AddOptions<LabOptions>().Bind(_configuration.GetSection(nameof(LabOptions)));

            // CTF API services
            services.AddCtfApiCryptoService();
            services.AddCtfApiClient();

            // Memory cache
            services.AddMemoryCache();
            
            // Lab configuration
            services.AddSingleton<ILabConfigurationService>(s =>
            {
                var labConfig = new LabConfigurationService(s.GetRequiredService<IOptionsMonitor<LabOptions>>());
                labConfig.ReadConfigurationAsync().GetAwaiter().GetResult();
                return labConfig;
            });

            // Lab state service
            services.AddSingleton<IStateService, StateService>();
            
            // Docker container support
            services.AddSingleton<IDockerService, DockerService>();

            // Change name of antiforgery cookie
            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "ctf4e-labserver.antiforgery";
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            // Authentication
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.Cookie.Name = "ctf4e-labserver.session";
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
            // Verbose stack traces
            if(_labOptions.DevelopmentMode)
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
