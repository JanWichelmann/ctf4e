using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ctf4e.Api.Options;
using Ctf4e.LabServer.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ctf4e.LabServer.Services;
using Newtonsoft.Json;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace Ctf4e.LabServer
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            host.Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, 5001, listenOptions =>
                        {
                            // Use HTTPS if there is a certificate specified in environment variables
                            string certName = Environment.GetEnvironmentVariable("CTF4E_LAB_CERT_FILE");
                            string certPassword = Environment.GetEnvironmentVariable("CTF4E_LAB_CERT_PASSWORD");
                            if(certName != null && File.Exists(certName))
                                listenOptions.UseHttps(certName, certPassword ?? "");
                        });
                    });
                })
                .ConfigureAppConfiguration((hostBuilderContext, config) =>
                {
                    // Load specified configuration file
                    string configFile = Environment.GetEnvironmentVariable("CTF4E_LAB_CONFIG_FILE");
                    if(configFile == null)
                        throw new Exception("No configuration file specified.");
                    config.AddJsonFile(configFile, false, true);
                });
    }
}
