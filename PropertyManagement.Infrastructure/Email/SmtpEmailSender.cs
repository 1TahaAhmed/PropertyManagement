using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PropertyManagement.Application.Abstractions.Email;
using MailKitSmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace PropertyManagement.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options) : IEmailSender
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

        await smtpClient.AuthenticateAsync(
            _options.Username,
            _options.Password,
            cancellationToken);

        await smtpClient.SendAsync(
            message,
            cancellationToken);

        await smtpClient.DisconnectAsync(
            true,
            cancellationToken);
    }
}
