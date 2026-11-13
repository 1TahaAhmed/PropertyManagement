using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.InviteMember;

public sealed class InviteMemberCommandHandler(
    IOrganizationRepository organizationRepository,
    IIdentityService identityService,
    IJwtTokenGenerator jwtTokenGenerator,
    IEmailSender emailSender,
    IClock clock,
    IUnitOfWork unitOfWork) : IRequestHandler<InviteMemberCommand, Result<InvitationResponse>>
{
    public async Task<Result<InvitationResponse>> Handle(
        InviteMemberCommand command,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(command.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<InvitationResponse>.Failure(
                new Error(
                    Code: "Organization.NotFound",
                    Message: "Organization was not found.",
                    Type: ErrorType.NotFound));
        }

        if (organization.Status != OrganizationStatus.Active)
        {
            return Result<InvitationResponse>.Failure(
                new Error(
                    Code: "Organization.NotActive",
                    Message: "Cannot invite members to an inactive organization.",
                    Type: ErrorType.Validation));
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var utcNow = clock.UtcNow;

        var existingUser = await identityService.FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            var existingMember = await organizationRepository.GetMemberAsync(
                command.OrganizationId,
                existingUser.UserId,
                cancellationToken);

            if (existingMember is not null && existingMember.Status == MembershipStatus.Active)
            {
                return Result<InvitationResponse>.Failure(
                    new Error(
                        Code: "Organization.AlreadyMember",
                        Message: "This user is already an active member of the organization.",
                        Type: ErrorType.Conflict));
            }
        }

        var existingPending = await organizationRepository.GetPendingInvitationByEmailAsync(
            command.OrganizationId,
            normalizedEmail,
            cancellationToken);

        if (existingPending is not null && existingPending.ExpiresAtUtc > utcNow)
        {
            return Result<InvitationResponse>.Failure(
                new Error(
                    Code: "Organization.DuplicateInvitation",
                    Message: "A pending invitation already exists for this email address.",
                    Type: ErrorType.Conflict));
        }

        var (plainToken, tokenHash, _) = jwtTokenGenerator.GenerateRefreshToken();
        var expiresAtUtc = utcNow.AddDays(7);

        var invitation = Invitation.Create(
            organizationId: command.OrganizationId,
            email: normalizedEmail,
            role: command.Role,
            tokenHash: tokenHash,
            expiresAtUtc: expiresAtUtc,
            utcNow: utcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await organizationRepository.AddInvitationAsync(invitation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var subject = $"Invitation to join {organization.Name}";
            var body =
                $"You have been invited to join {organization.Name} as {command.Role}.\n\n" +
                $"Invitation Token: {plainToken}\n\n" +
                $"This invitation expires on {expiresAtUtc:yyyy-MM-dd HH:mm} UTC.";

            await emailSender.SendAsync(normalizedEmail, subject, body, cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<InvitationResponse>.Failure(
                new Error(
                    Code: "Organization.InvitationEmailFailed",
                    Message: "Failed to send invitation email. Please try again later.",
                    Type: ErrorType.Failure));
        }

        await transaction.CommitAsync(cancellationToken);

        return Result<InvitationResponse>.Success(new InvitationResponse(
            Id: invitation.Id,
            OrganizationId: invitation.OrganizationId,
            Email: invitation.Email,
            Role: invitation.Role,
            Status: invitation.Status,
            ExpiresAtUtc: invitation.ExpiresAtUtc,
            CreatedAtUtc: invitation.CreatedAtUtc,
            RespondedAtUtc: invitation.RespondedAtUtc));
    }
}
