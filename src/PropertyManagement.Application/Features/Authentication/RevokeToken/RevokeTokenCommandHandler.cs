using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.RevokeToken;

public sealed class RevokeTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IClock clock) : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(
        RevokeTokenCommand command,
        CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenGenerator.HashToken(command.RefreshToken);

        var existingToken = await refreshTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (existingToken is not null && !existingToken.IsRevoked)
        {
            existingToken.Revoke(clock.UtcNow);
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
