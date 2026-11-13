using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Invitations.Revoke;

public sealed class RevokeInvitationCommandHandler(
    IOrganizationRepository organizationRepository,
    IClock clock) : IRequestHandler<RevokeInvitationCommand, Result>
{
    public async Task<Result> Handle(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var invitation = await organizationRepository.GetInvitationByIdAsync(
            command.InvitationId,
            command.OrganizationId,
            cancellationToken);

        if (invitation is null)
        {
            return Result.Failure(
                new Error(
                    Code: "Invitation.NotFound",
                    Message: "Invitation was not found.",
                    Type: ErrorType.NotFound));
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return Result.Failure(
                new Error(
                    Code: "Invitation.NotPending",
                    Message: $"Only pending invitations can be revoked. Current status: {invitation.Status}.",
                    Type: ErrorType.Conflict));
        }

        invitation.Revoke(clock.UtcNow);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
