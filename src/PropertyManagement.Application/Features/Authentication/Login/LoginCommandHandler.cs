using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;
using RefreshTokenEntity = PropertyManagement.Domain.Entities.Authentication.RefreshToken;

namespace PropertyManagement.Application.Features.Authentication.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    IOrganizationRepository organizationRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenRepository refreshTokenRepository,
    IClock clock) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var credentialsResult = await identityService.ValidateCredentialsAsync(
            command.Email,
            command.Password,
            cancellationToken);

        if (credentialsResult.IsFailure)
        {
            return Result<LoginResponse>.Failure(credentialsResult.Errors.ToArray());
        }

        var user = credentialsResult.Value;

        // Resolve Organization and Role
        var userOrganizations = await organizationRepository.GetUserOrganizationsAsync(
            user.UserId,
            cancellationToken);

        Guid? activeOrgId = null;
        string? activeRole = null;

        if (command.OrganizationId.HasValue)
        {
            var matched = userOrganizations.FirstOrDefault(o => o.Organization.Id == command.OrganizationId.Value);
            if (matched.Organization is null)
            {
                return Result<LoginResponse>.Failure(
                    new Error(
                        Code: "Authentication.NotOrganizationMember",
                        Message: "User is not a member of the requested organization.",
                        Type: ErrorType.Validation));
            }

            activeOrgId = matched.Organization.Id;
            activeRole = matched.Roles.Count > 0 ? matched.Roles[0].ToString() : null;
        }
        else if (userOrganizations.Count > 0)
        {
            var primary = userOrganizations.FirstOrDefault(o => o.Organization.Status == OrganizationStatus.Active);
            if (primary.Organization is not null)
            {
                activeOrgId = primary.Organization.Id;
                activeRole = primary.Roles.Count > 0 ? primary.Roles[0].ToString() : null;
            }
        }

        // Generate Tokens
        var accessToken = jwtTokenGenerator.GenerateAccessToken(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            activeOrgId,
            activeRole);

        var (plainRefreshToken, tokenHash, expiresAtUtc) = jwtTokenGenerator.GenerateRefreshToken();

        var refreshTokenEntity = RefreshTokenEntity.Create(
            user.UserId,
            tokenHash,
            expiresAtUtc,
            clock.UtcNow);

        await refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            AccessToken: accessToken,
            RefreshToken: plainRefreshToken,
            ExpiresAtUtc: expiresAtUtc,
            OrganizationId: activeOrgId,
            Role: activeRole);

        return Result<LoginResponse>.Success(response);
    }
}
