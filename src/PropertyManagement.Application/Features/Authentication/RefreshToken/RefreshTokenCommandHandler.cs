using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Entities.Authentication;
using PropertyManagement.Domain.Enums;

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

        // Reuse Detection: If the token was already revoked, an attacker or compromised client is reusing it
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

        // Rotate Token: create new, mark old as revoked
        var (newPlainToken, newTokenHash, newExpiresAtUtc) = jwtTokenGenerator.GenerateRefreshToken();

        existingToken.Revoke(clock.UtcNow, replacedByTokenHash: newTokenHash);

        var newRefreshToken = PropertyManagement.Domain.Entities.Authentication.RefreshToken.Create(
            existingToken.UserId,
            newTokenHash,
            newExpiresAtUtc,
            clock.UtcNow);

        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        // Fetch User and Active Organization to generate new Access Token
        var userResult = await identityService.GetAuthUserByIdAsync(
            existingToken.UserId,
            cancellationToken);

        if (userResult.IsFailure)
        {
            return Result<RefreshTokenResponse>.Failure(userResult.Errors);
        }

        var user = userResult.Value;

        var userOrganizations = await organizationRepository.GetUserOrganizationsAsync(
            user.UserId,
            cancellationToken);

        Guid? activeOrgId = null;
        string? activeRole = null;

        if (userOrganizations.Count > 0)
        {
            var primary = userOrganizations.FirstOrDefault(o => o.Organization.Status == OrganizationStatus.Active);
            if (primary.Organization is not null)
            {
                activeOrgId = primary.Organization.Id;
                activeRole = primary.Roles.Count > 0 ? primary.Roles[0].ToString() : null;
            }
        }

        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            activeOrgId,
            activeRole);

        var response = new RefreshTokenResponse(
            AccessToken: newAccessToken,
            RefreshToken: newPlainToken,
            ExpiresAtUtc: newExpiresAtUtc);

        return Result<RefreshTokenResponse>.Success(response);
    }
}
