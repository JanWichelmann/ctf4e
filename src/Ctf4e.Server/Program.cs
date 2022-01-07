using System;
using System.Threading.Tasks;
using Ctf4e.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ctf4e.Server;

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
        }

        await host.RunAsync();
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
                string configFile = Environment.GetEnvironmentVariable("CTF4E_CONFIG_FILE");
                if(configFile == null)
                    throw new Exception("No configuration file specified.");
                config.AddJsonFile(configFile);
            });
}