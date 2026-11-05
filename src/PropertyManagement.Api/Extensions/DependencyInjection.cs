using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Api.Authorization;

namespace PropertyManagement.Api.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApi(
            this IServiceCollection services)
        {
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

            return services;
        }
    }
}
