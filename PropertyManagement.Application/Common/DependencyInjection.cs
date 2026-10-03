using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography.X509Certificates;

namespace PropertyManagement.Application.Common
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(
                    typeof(DependencyInjection).Assembly);

            });

            return services;
        }
    }
}
