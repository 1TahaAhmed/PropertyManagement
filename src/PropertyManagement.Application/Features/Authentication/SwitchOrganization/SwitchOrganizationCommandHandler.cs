using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.Login;
using PropertyManagement.Domain.Enums;
using RefreshTokenEntity = PropertyManagement.Domain.Entities.Authentication.RefreshToken;

namespace PropertyManagement.Application.Features.Authentication.SwitchOrganization;

public sealed class SwitchOrganizationCommandHandler(
    IIdentityService identityService,
    IOrganizationRepository organizationRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenRepository refreshTokenRepository,
    IClock clock) : IRequestHandler<SwitchOrganizationCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        SwitchOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var userResult = await identityService.GetAuthUserByIdAsync(command.UserId, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result<LoginResponse>.Failure(userResult.Errors.ToArray());
        }

        var user = userResult.Value;

        var userOrganizations = await organizationRepository.GetUserOrganizationsAsync(
            user.UserId,
            cancellationToken);

        var matched = userOrganizations.FirstOrDefault(o =>
            o.Organization.Id == command.TargetOrganizationId &&
            o.Organization.Status == OrganizationStatus.Active);

        if (matched.Organization is null)
        {
            return Result<LoginResponse>.Failure(
                new Error(
                    Code: "Authentication.NotOrganizationMember",
                    Message: "User is not an active member of the requested organization.",
                    Type: ErrorType.Forbidden));
        }

        var activeRole = matched.Roles.Count > 0 ? matched.Roles[0].ToString() : null;

        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenGenerator.GenerateAccessToken(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            matched.Organization.Id,
            activeRole);

        var (plainRefreshToken, tokenHash, refreshTokenExpiresAtUtc) = jwtTokenGenerator.GenerateRefreshToken();

        if (!string.IsNullOrWhiteSpace(command.CurrentRefreshToken))
        {
            var currentTokenHash = jwtTokenGenerator.HashToken(command.CurrentRefreshToken.Trim());
            var existingToken = await refreshTokenRepository.GetByTokenHashAsync(
                currentTokenHash,
                cancellationToken);

            if (existingToken is not null &&
                existingToken.UserId == user.UserId &&
                !existingToken.IsRevoked)
            {
                existingToken.Revoke(clock.UtcNow, replacedByTokenHash: tokenHash);
            }
        }

        var newRefreshToken = RefreshTokenEntity.Create(
            userId: user.UserId,
            tokenHash: tokenHash,
            expiresAtUtc: refreshTokenExpiresAtUtc,
            utcNow: clock.UtcNow,
            organizationId: matched.Organization.Id);

        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: plainRefreshToken,
            ExpiresAtUtc: accessTokenExpiresAtUtc,
            OrganizationId: matched.Organization.Id,
            Role: activeRole,
            RefreshTokenExpiresAtUtc: refreshTokenExpiresAtUtc));
    }
}
