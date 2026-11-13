using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler(
    IOrganizationRepository organizationRepository,
    IIdentityService identityService,
    IJwtTokenGenerator jwtTokenGenerator,
    IClock clock) : IRequestHandler<AcceptInvitationCommand, Result<AcceptInvitationResponse>>
{
    public async Task<Result<AcceptInvitationResponse>> Handle(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenGenerator.HashToken(command.Token.Trim());
        var invitation = await organizationRepository.GetInvitationByTokenHashAsync(tokenHash, cancellationToken);

        if (invitation is null)
        {
            return Result<AcceptInvitationResponse>.Failure(
                new Error(
                    Code: "Organization.InvitationNotFound",
                    Message: "The invitation token is invalid.",
                    Type: ErrorType.NotFound));
        }

        var userEmail = command.UserEmail;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            var userResult = await identityService.GetAuthUserByIdAsync(command.UserId, cancellationToken);
            if (userResult.IsFailure)
            {
                return Result<AcceptInvitationResponse>.Failure(userResult.Errors.ToArray());
            }

            userEmail = userResult.Value.Email;
        }

        if (!string.Equals(invitation.Email, userEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<AcceptInvitationResponse>.Failure(
                new Error(
                    Code: "Organization.InvitationEmailMismatch",
                    Message: "This invitation was issued for a different email address.",
                    Type: ErrorType.Forbidden));
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return Result<AcceptInvitationResponse>.Failure(
                new Error(
                    Code: "Organization.InvitationNotPending",
                    Message: $"Invitation cannot be accepted because its status is {invitation.Status}.",
                    Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;
        if (utcNow > invitation.ExpiresAtUtc)
        {
            return Result<AcceptInvitationResponse>.Failure(
                new Error(
                    Code: "Organization.InvitationExpired",
                    Message: "This invitation has expired.",
                    Type: ErrorType.Validation));
        }

        var organization = await organizationRepository.GetByIdAsync(invitation.OrganizationId, cancellationToken);
        if (organization is null || organization.Status != OrganizationStatus.Active)
        {
            return Result<AcceptInvitationResponse>.Failure(
                new Error(
                    Code: "Organization.NotActive",
                    Message: "The organization for this invitation is not active.",
                    Type: ErrorType.Validation));
        }

        var existingMember = await organizationRepository.GetMemberAsync(
            invitation.OrganizationId,
            command.UserId,
            cancellationToken);

        if (existingMember is not null)
        {
            if (existingMember.Status == MembershipStatus.Active)
            {
                return Result<AcceptInvitationResponse>.Failure(
                    new Error(
                        Code: "Organization.AlreadyMember",
                        Message: "You are already an active member of this organization.",
                        Type: ErrorType.Conflict));
            }

            existingMember.Activate(utcNow);
            existingMember.ChangeRole(invitation.Role, utcNow);
        }
        else
        {
            var newMember = OrganizationMember.Create(
                organizationId: invitation.OrganizationId,
                userId: command.UserId,
                role: invitation.Role,
                utcNow: utcNow);

            await organizationRepository.AddMemberAsync(newMember, cancellationToken);
        }

        invitation.Accept(utcNow);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result<AcceptInvitationResponse>.Success(new AcceptInvitationResponse(
            OrganizationId: organization.Id,
            OrganizationName: organization.Name,
            Role: invitation.Role,
            JoinedAtUtc: utcNow));
    }
}
