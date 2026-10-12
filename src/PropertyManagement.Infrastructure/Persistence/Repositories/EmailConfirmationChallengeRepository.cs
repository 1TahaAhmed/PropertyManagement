using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Infrastructure.Persistence.Repositories
{
    public sealed class EmailConfirmationChallengeRepository(
        ApplicationDbContext dbContext)
        : IEmailConfirmationChallengeRepository
    {
        public Task<EmailConfirmationChallenge?> GetActiveByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return dbContext.EmailConfirmationChallenges
                 .SingleOrDefaultAsync(
                        challenge =>
                            challenge.UserId == userId
                            && challenge.UsedAt == null
                            && challenge.RevokedAt == null
                            && challenge.ExpiresAt > utcNow
                            && challenge.FailedAttempts 
                                < EmailConfirmationChallenge
                                    .MaxFailedAttempts,
                        cancellationToken
                    );
        }

        public async Task AddAsync(
            EmailConfirmationChallenge challenge,
            CancellationToken cancellationToken = default)
                {
                    ArgumentNullException.ThrowIfNull(challenge);

            await dbContext.EmailConfirmationChallenges.AddAsync(
                    challenge,
                    cancellationToken
                    );
                }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
