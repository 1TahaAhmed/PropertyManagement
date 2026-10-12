using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationChallengeRepository
    {
        // seeking for an active challenge for a user
        Task<EmailConfirmationChallenge?> GetActiveByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default
            );
        // adding a new challenge for a user
        Task AddAsync(
            EmailConfirmationChallenge challenge,
            CancellationToken cancellationToken = default
            );

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default
            );
    }
}
