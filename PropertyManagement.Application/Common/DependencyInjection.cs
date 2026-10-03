using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Common.Behaviors;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.Registeration;
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

            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddTransient<
                IPipelineBehavior<RegisterCommand, Result<RegisterResponse>>,
                ValidationBehavior<RegisterCommand, RegisterResponse>>();


            return services;
        }
    }
}
