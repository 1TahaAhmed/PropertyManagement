using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;
using RefreshTokenEntity = PropertyManagement.Domain.Entities.Authentication.RefreshToken;

namespace PropertyManagement.Application.Features.Authentication.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IIdentityService identityService,
    IOrganizationRepository organizationRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IClock clock) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenGenerator.HashToken(command.RefreshToken);

        var existingToken = await refreshTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (existingToken is null)
        {
            return Result<RefreshTokenResponse>.Failure(
                new Error(
                    Code: "Authentication.InvalidRefreshToken",
                    Message: "The provided refresh token is invalid.",
                    Type: ErrorType.Unauthorized));
        }

        if (existingToken.IsRevoked)
        {
            await refreshTokenRepository.RevokeAllUserTokensAsync(
                existingToken.UserId,
                clock.UtcNow,
                cancellationToken);

            await refreshTokenRepository.SaveChangesAsync(cancellationToken);

            return Result<RefreshTokenResponse>.Failure(
                new Error(
                    Code: "Authentication.CompromisedRefreshToken",
                    Message: "Suspicious token reuse detected. All active sessions for this account have been terminated.",
                    Type: ErrorType.Unauthorized));
        }

        if (existingToken.IsExpired(clock.UtcNow))
        {
            return Result<RefreshTokenResponse>.Failure(
                new Error(
                    Code: "Authentication.ExpiredRefreshToken",
                    Message: "The refresh token has expired. Please log in again.",
                    Type: ErrorType.Unauthorized));
        }

        var (newPlainToken, newTokenHash, newRefreshTokenExpiresAtUtc) = jwtTokenGenerator.GenerateRefreshToken();

        existingToken.Revoke(clock.UtcNow, replacedByTokenHash: newTokenHash);

        var newRefreshToken = RefreshTokenEntity.Create(
            existingToken.UserId,
            newTokenHash,
            newRefreshTokenExpiresAtUtc,
            clock.UtcNow,
            existingToken.OrganizationId);

        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var userResult = await identityService.GetAuthUserByIdAsync(
            existingToken.UserId,
            cancellationToken);

        if (userResult.IsFailure)
        {
            return Result<RefreshTokenResponse>.Failure(userResult.Errors.ToArray());
        }

        var user = userResult.Value;

        var userOrganizations = await organizationRepository.GetUserOrganizationsAsync(
            user.UserId,
            cancellationToken);

        Guid? activeOrgId = null;
        string? activeRole = null;

        if (existingToken.OrganizationId.HasValue)
        {
            var matched = userOrganizations.FirstOrDefault(o =>
                o.Organization.Id == existingToken.OrganizationId.Value &&
                o.Organization.Status == OrganizationStatus.Active);

            if (matched.Organization is not null)
            {
                activeOrgId = matched.Organization.Id;
                activeRole = matched.Roles.Count > 0 ? matched.Roles[0].ToString() : null;
            }
        }

        if (!activeOrgId.HasValue && userOrganizations.Count > 0)
        {
            var primary = userOrganizations.FirstOrDefault(o => o.Organization.Status == OrganizationStatus.Active);
            if (primary.Organization is not null)
            {
                activeOrgId = primary.Organization.Id;
                activeRole = primary.Roles.Count > 0 ? primary.Roles[0].ToString() : null;
            }
        }

        var (newAccessToken, newAccessTokenExpiresAtUtc) = jwtTokenGenerator.GenerateAccessToken(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            activeOrgId,
            activeRole);

        var response = new RefreshTokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: newPlainToken,
            ExpiresAtUtc: newAccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc: newRefreshTokenExpiresAtUtc);

        return Result<RefreshTokenResponse>.Success(response);
    }
}
