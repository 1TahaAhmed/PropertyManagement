using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Abstractions.Email
{
    public interface IEmailConfirmationChallengeRepository
    {
        Task<EmailConfirmationChallenge?> GetActiveByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default
            );
        Task AddAsync(
            EmailConfirmationChallenge challenge,
            CancellationToken cancellationToken = default
            );

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default
            );
    }
}
