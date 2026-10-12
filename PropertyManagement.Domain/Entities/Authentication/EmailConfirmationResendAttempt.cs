namespace PropertyManagement.Domain.Entities.Authentication
{
    public sealed class EmailConfirmationResendAttempt
    {
        private EmailConfirmationResendAttempt() { }

        private EmailConfirmationResendAttempt(
            Guid id,
            Guid userId,
            string ipAddress,
            DateTime requestedAt)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException(
                    "User ID cannot be empty.",
                    nameof(userId));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);

            Id = id;
            UserId = userId;
            IpAddress = ipAddress;
            RequestedAt = requestedAt;
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public string IpAddress { get; private set; } = string.Empty;

        public DateTime RequestedAt { get; private set; }

        public static EmailConfirmationResendAttempt Create(
        Guid userId,
        string ipAddress,
        DateTime requestedAt)
        {
            return new EmailConfirmationResendAttempt(
                id: Guid.NewGuid(),
                userId: userId,
                ipAddress: ipAddress,
                requestedAt: requestedAt);
        }
    }
}
