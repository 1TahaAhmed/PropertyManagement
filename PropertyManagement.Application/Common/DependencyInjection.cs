using Microsoft.Extensions.DependencyInjection;

namespace PropertyManagement.Application.Common
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            return services;
        }
    }
}
