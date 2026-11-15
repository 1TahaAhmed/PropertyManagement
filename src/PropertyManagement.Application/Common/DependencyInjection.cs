using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Common.Behaviors;
using PropertyManagement.Application.Services.EmailConfirmation;
using PropertyManagement.Domain.Entities.Billing.Services;

namespace PropertyManagement.Application.Common;

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

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        services.AddScoped<
            IEmailConfirmationCodeIssuer,
            EmailConfirmationCodeIssuer>();

        services.AddSingleton<IBillingCalculationEngine, BillingCalculationEngine>();

        return services;
    }
}
