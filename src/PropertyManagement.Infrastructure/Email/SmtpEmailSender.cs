using System.Text.RegularExpressions;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PropertyManagement.Application.Abstractions.Email;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace PropertyManagement.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger,
    IHostEnvironment environment) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(body);

        cancellationToken.ThrowIfCancellationRequested();

        if (environment.IsDevelopment() &&
            (string.IsNullOrWhiteSpace(_options.Host) ||
             _options.Host.Equals("smtp.example.com", StringComparison.OrdinalIgnoreCase) ||
             _options.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            var match = Regex.Match(body, @"([0-9]{6})");
            var tokenMatch = Regex.Match(body, @"Invitation Token:\s*(\S+)");
            var codeNotice = match.Success
                ? $" [VERIFICATION CODE: {match.Value}]"
                : tokenMatch.Success
                    ? $" [INVITATION TOKEN: {tokenMatch.Groups[1].Value}]"
                    : string.Empty;

            logger.LogInformation("📧 [DEV EMAIL MOCK] To: {Recipient} | Subject: {Subject}{CodeNotice}", recipient, subject, codeNotice);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = body };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new MailKitSmtpClient();
            var secureSocketOption = _options.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

            await smtpClient.ConnectAsync(_options.Host, _options.Port, secureSocketOption, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await smtpClient.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await smtpClient.SendAsync(message, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", recipient);

            if (!environment.IsDevelopment())
            {
                throw;
            }

            logger.LogWarning("Email delivery failed in development environment for {Recipient}", recipient);
        }
    }
}
