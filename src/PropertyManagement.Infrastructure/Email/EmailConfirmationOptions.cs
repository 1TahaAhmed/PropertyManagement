using PropertyManagement.Application.Abstractions.Email;

namespace PropertyManagement.Infrastructure.Email;

public sealed class EmailConfirmationOptions
    : IEmailConfirmationSettings
{
    public const string SectionName = "Email:Confirmation";

    public string BaseUrl { get; init; } = string.Empty;
    public int CodeLifetimeMinutes { get; init; } = 10;
    public string? HmacSecret { get; init; }
}
