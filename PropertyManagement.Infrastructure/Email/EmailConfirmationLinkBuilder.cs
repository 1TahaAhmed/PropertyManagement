using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Email;

namespace PropertyManagement.Infrastructure.Email;

public sealed class EmailConfirmationLinkBuilder(
    IOptions<EmailConfirmationOptions> options)
    : IEmailConfirmationLinkBuilder
{
    private readonly EmailConfirmationOptions _options = options.Value;

    public string Build(
        Guid userId,
        string confirmationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            _options.BaseUrl);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            confirmationToken);

        var endpoint =
        $"{_options.BaseUrl.TrimEnd('/')}/api/v1/email-confirmation/start";

        return QueryHelpers.AddQueryString(
            endpoint,
            new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = confirmationToken
            });
    }
}
