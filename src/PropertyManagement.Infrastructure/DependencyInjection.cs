using PropertyManagement.Application.Abstractions.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Abstractions.Tenants;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Infrastructure.Common.Security;
using PropertyManagement.Infrastructure.Common.Time;
using PropertyManagement.Infrastructure.Email;
using PropertyManagement.Infrastructure.Identity;
using PropertyManagement.Infrastructure.Persistence;
using PropertyManagement.Infrastructure.Persistence.Repositories;

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

            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlServer(connectionString)
            );

            services.AddDataProtection();
            services.AddSingleton<IClock, SystemClock>();

            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Secret) && options.Secret.Length >= 32,
                    "JWT secret must be at least 32 characters long.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Issuer),
                    "JWT issuer is required.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Audience),
                    "JWT audience is required.")
                .ValidateOnStart();

            services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            var jwtSection = configuration.GetSection(JwtOptions.SectionName);
            var secretKey = jwtSection["Secret"];
            if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT Secret configuration is missing or shorter than 32 characters. Configure 'Jwt:Secret' via environment variables or user secrets.");
            }
            var issuer = jwtSection["Issuer"] ?? "PropertyManagement";
            var audience = jwtSection["Audience"] ?? "PropertyManagement";

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        var problemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Authentication credentials are required to access this resource. Please provide a valid Bearer token.",
                            Instance = context.Request.Path
                        };

                        await context.Response.WriteAsJsonAsync(problemDetails);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";

                        var problemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "Forbidden",
                            Detail = "You do not have the required permissions to access this resource.",
                            Instance = context.Request.Path
                        };

                        await context.Response.WriteAsJsonAsync(problemDetails);
                    }
                };
            });

            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.Host),
                    "SMTP host is required.")
                .Validate(options =>
                    options.Port is > 0 and <= 65535,
                    "SMTP port must be between 1 and 65535.")
                .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.FromEmail),
                    "SMTP from email is required.")
                .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.FromName),
                    "SMTP from name is required.")
                .ValidateOnStart();

            services.AddScoped<IEmailSender, SmtpEmailSender>();

            services.AddOptions<EmailConfirmationOptions>()
                .Bind(configuration.GetSection(
                    EmailConfirmationOptions.SectionName))
                .Validate(options =>
                    !string.IsNullOrWhiteSpace(options.BaseUrl),
                    "Email confirmation base URL is required.")
                .Validate(options =>
                    options.CodeLifetimeMinutes is > 0 and <= 60,
                    "Email confirmation code lifetime must be between 1 and 60 minutes.")
                .ValidateOnStart();

            services.AddSingleton<IEmailConfirmationSettings>(
                serviceProvider =>
                    serviceProvider
                        .GetRequiredService<
                            IOptions<EmailConfirmationOptions>>()
                        .Value);

            services.AddOptions<EmailConfirmationResendOptions>()
                .Bind(configuration.GetSection(
                    EmailConfirmationResendOptions.SectionName))
                .Validate(
                    options => options.CooldownSeconds >= 30,
                        "Resend cooldown must be at least 30 seconds.")
                .Validate(
                    options => options.WindowMinutes > 0,
                        "Resend window must be positive.")
                .Validate(
                    options => options.MaxRequestsPerWindow > 0,
                        "Maximum resend requests per window must be positive.")
                .Validate(
                    options =>
                        options.MaxRequestsPerDay >=
                        options.MaxRequestsPerWindow,
                            "Daily resend limit must not be lower than window limit.")
                .ValidateOnStart();

            services.AddSingleton<IEmailConfirmationResendSettings>(
                serviceProvider =>
                    serviceProvider
                        .GetRequiredService<
                            IOptions<EmailConfirmationResendOptions>>()
                        .Value);

            services.AddScoped<
                IEmailConfirmationChallengeRepository,
                EmailConfirmationChallengeRepository>();

            services.AddScoped<
                IEmailConfirmationResendPolicy,
                EmailConfirmationResendPolicy>();

            services.AddSingleton<
                IVerificationCodeGenerator,
                RandomVerificationCodeGenerator>();

            services.AddSingleton<
                IVerificationCodeProtector,
                DataProtectionVerificationCodeProtector>();

            services.AddScoped<IPropertyRepository, PropertyRepository>();
            services.AddScoped<IResidentRepository, ResidentRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<IUnitRepository, UnitRepository>();
            services.AddScoped<IOrganizationRepository, OrganizationRepository>();
            services.AddScoped<ILeaseRepository, LeaseRepository>();
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

            return services;
        }
    }
}
