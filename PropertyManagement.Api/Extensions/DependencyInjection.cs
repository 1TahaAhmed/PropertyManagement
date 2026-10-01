using Microsoft.Extensions.DependencyInjection;

namespace PropertyManagement.Api.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApi(
            this IServiceCollection services)
        {
            return services;
        }
    }
}
