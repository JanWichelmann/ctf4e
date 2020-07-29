using Ctf4e.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ctf4e.Api.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCtfApiClient(this IServiceCollection services)
        {
            return services.AddTransient<ICtfApiClient, CtfApiClient>();
        }

        public static IServiceCollection AddCtfApiCryptoService(this IServiceCollection services)
        {
            return services.AddSingleton<ICryptoService, CryptoService>();
        }
    }
}