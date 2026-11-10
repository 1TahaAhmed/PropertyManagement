using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PropertyManagement.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the accessToken returned by POST /api/v1/Auth/login. Do not include the 'Bearer ' prefix; Swagger UI adds it automatically.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            // Attach the Bearer requirement to protected operations only.
            // A document-wide requirement incorrectly marks public auth endpoints as secured.
            options.OperationFilter<BearerAuthorizationOperationFilter>();
        });

        return services;
    }
}

public sealed class BearerAuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        // AllowAnonymous takes precedence over controller-level Authorize metadata.
        if (endpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        if (!endpointMetadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer"),
                    new List<string>()
                }
            }
        };
    }
}
