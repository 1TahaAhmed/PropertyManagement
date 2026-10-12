namespace PropertyManagement.Infrastructure.Email
{
    public sealed class SmtpOptions
    {
        public const string SectionName = "Email:Smtp";
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; } = 587;
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string FromEmail { get; init; } = string.Empty;
        public string FromName { get; init; } = string.Empty;
        public bool UseSsl { get; init; } = true;
    }
}
