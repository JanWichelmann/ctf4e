using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Ctf4e.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/*
 * TODO Make Favicon/CSS configurable
 */

namespace Ctf4e.Server
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using(var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var services = scope.ServiceProvider;

                // Ensure database is initialized and up-to-date
                var dbContext = services.GetRequiredService<CtfDbContext>();
                await dbContext.Database.MigrateAsync();

                // Initialize singletons
                await services.GetRequiredService<IFlagPointService>().ReloadAsync(services.GetRequiredService<IConfigurationService>());
            }

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, 5000, listenOptions =>
                        {
                            // Use HTTPS if there is a certificate specified in environment variables
                            string certName = Environment.GetEnvironmentVariable("CTF4E_CERT_FILE");
                            string certPassword = Environment.GetEnvironmentVariable("CTF4E_CERT_PASSWORD");
                            if(certName != null && File.Exists(certName))
                                listenOptions.UseHttps(certName, certPassword ?? "");
                        });
                    });
                })
                .ConfigureAppConfiguration((hostBuilderContext, config) =>
                {
                    // Load specified configuration file
                    string configFile = Environment.GetEnvironmentVariable("CTF4E_CONFIG_FILE");
                    if(configFile == null)
                        throw new Exception("No configuration file specified.");
                    config.AddJsonFile(configFile);
                });
    }
}
