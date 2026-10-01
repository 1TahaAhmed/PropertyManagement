using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Infrastructure.Persistence;

namespace PropertyManagement.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("MyConn")
                ?? throw new InvalidOperationException("Connection string 'MyConn' was not found.");

            services.AddDbContext<ApplicationDbContext>
            (
                options => options.UseSqlServer(connectionString)
            );
                
            return services;
        }
    }
}
