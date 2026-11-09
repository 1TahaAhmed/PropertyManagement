using System.Text.RegularExpressions;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PropertyManagement.Application.Abstractions.Email;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace PropertyManagement.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
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

        // Extract verification code if present in body for easy local development testing
        var match = Regex.Match(body, @"([0-9]{6})");
        var codeNotice = match.Success ? $" [VERIFICATION CODE: {match.Value}]" : string.Empty;

        // Fallback / Development mode when SMTP host is unconfigured, localhost, or example.com
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            _options.Host.Equals("smtp.example.com", StringComparison.OrdinalIgnoreCase) ||
            _options.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("⚠️ [DEVELOPMENT EMAIL SENDER] SMTP not configured. Suppressing network send to {Recipient}. Subject: {Subject}{CodeNotice}", recipient, subject, codeNotice);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"
=========================================
📧 [EMAIL MOCK / DEV] To: {recipient}
Subject: {subject}
{codeNotice}
=========================================
");
            Console.ResetColor();
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _options.FromName,
                _options.FromEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new MailKitSmtpClient();
            var secureSocketOption = _options.UseSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await smtpClient.ConnectAsync(
                _options.Host,
                _options.Port,
                secureSocketOption,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await smtpClient.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);
            }

            await smtpClient.SendAsync(
                message,
                cancellationToken);

            await smtpClient.DisconnectAsync(
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Do not break user registration during development if the SMTP credentials fail or are blocked
            logger.LogError(ex, "❌ [SMTP SEND FAILED] Failed to send email to {Recipient}. Falling back to console output: {CodeNotice}", recipient, codeNotice);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"
⚠️ [SMTP FAILED -> FALLBACK DEV CONSOLE]
Recipient: {recipient}
Subject: {subject}
{codeNotice}
Error: {ex.Message}
");
            Console.ResetColor();
        }
    }
}
