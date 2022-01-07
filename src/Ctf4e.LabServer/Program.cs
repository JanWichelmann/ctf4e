using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ctf4e.LabServer;

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
            })
            .ConfigureAppConfiguration((_, config) =>
            {
                // Load specified configuration file
                string configFile = Environment.GetEnvironmentVariable("CTF4E_LAB_CONFIG_FILE");
                if(configFile == null)
                    throw new Exception("No configuration file specified.");
                config.AddJsonFile(configFile, false, true);
            });
}