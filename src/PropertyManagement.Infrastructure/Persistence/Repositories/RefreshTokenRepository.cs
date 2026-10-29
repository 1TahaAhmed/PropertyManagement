using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(ApplicationDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        await context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > utcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke(utcNow);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
