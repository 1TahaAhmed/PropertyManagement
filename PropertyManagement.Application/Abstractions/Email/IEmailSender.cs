namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailSender
    {
        Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default
        );
    }
}
