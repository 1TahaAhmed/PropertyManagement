using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Infrastructure.Common.Time;
using PropertyManagement.Infrastructure.Email;
using PropertyManagement.Infrastructure.Identity;
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
                .ValidateOnStart();

            services.AddScoped<
                IEmailConfirmationLinkBuilder,
                EmailConfirmationLinkBuilder>();


            return services;
        }
    }
}
