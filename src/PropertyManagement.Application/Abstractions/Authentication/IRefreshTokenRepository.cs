using PropertyManagement.Domain.Entities.Authentication;

namespace PropertyManagement.Application.Abstractions.Authentication;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task RevokeAllUserTokensAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
