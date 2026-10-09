namespace PropertyManagement.Domain.Entities.Authentication
{
    public sealed class EmailConfirmationChallenge
    {
        public const int CodeLength = 6;
        public const int MaxFailedAttempts = 5;

        private EmailConfirmationChallenge() { }

        public EmailConfirmationChallenge(
            Guid id,
            Guid userId,
            string protectedCode,
            DateTime createdAt,
            DateTime expiresAt
            )
        {
            Id = id;
            UserId = userId;
            ProtectedCode = protectedCode;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string ProtectedCode { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public int FailedAttempts { get; private set; }
        public DateTime? UsedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public string? RevocationReason { get; private set; }
        public bool IsUsed => UsedAt.HasValue;
        public bool IsRevoked => RevokedAt.HasValue;
        public bool IsExpired(DateTime utcNow)
        {
            return utcNow >= ExpiresAt;
        }

        public bool HasExceededAttempts()
        {
            return FailedAttempts >= MaxFailedAttempts;
        }

        public bool IsActive(DateTime utcNow)
        {
            return !IsUsed
                && !IsRevoked
                && !IsExpired(utcNow)
                && !HasExceededAttempts();
        }

        public static EmailConfirmationChallenge Create(
            Guid userId,
            string protectedCode,
            DateTime createdAt,
            DateTime expiresAt
            )
        {
            if(userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(
            protectedCode);

            if (expiresAt <= createdAt)
            {
                throw new ArgumentException(
                    "Expiration must be after creation.",
                    nameof(expiresAt));
            }

            return new EmailConfirmationChallenge(
                id: Guid.NewGuid(),
                userId: userId,
                protectedCode: protectedCode,
                createdAt: createdAt,
                expiresAt: expiresAt);
        }

        public void RegisterFailedAttempt(DateTime utcNow)
        {
            if (!IsActive(utcNow))
            {
                throw new InvalidOperationException(
                    "An inactive challenge cannot register an attempt.");
            }

            FailedAttempts++;
        }

        public void MarkAsUsed(DateTime usedAt)
        {
            if (!IsActive(usedAt))
            {
                throw new InvalidOperationException(
                    "Only an active challenge can be marked as used.");
            }

            UsedAt = usedAt;
        }

        public void Revoke(
            DateTime revokedAt,
            string reason)
        {
            if (IsUsed)
            {
                throw new InvalidOperationException(
                    "A used challenge cannot be revoked.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            RevokedAt = revokedAt;
            RevocationReason = reason;
        }
    }
}