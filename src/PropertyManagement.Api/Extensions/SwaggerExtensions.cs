using Microsoft.OpenApi.Models;

namespace PropertyManagement.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "JWT Authorization header using the Bearer scheme. Enter token directly or as 'Bearer {token}'",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiSecuritySchemeReference("Bearer")
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            // Add global security requirement so ALL endpoints in Swagger get the lock icon
            options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                {
                    securityScheme,
                    new List<string>()
                }
            });
        });

        return services;
    }
}
