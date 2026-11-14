using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed class ChangeMemberRoleCommandHandler(
    IOrganizationRepository organizationRepository,
    IClock clock) : IRequestHandler<ChangeMemberRoleCommand, Result>
{
    public async Task<Result> Handle(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ActorRole is not (MembershipRole.Owner or MembershipRole.Admin))
        {
            return Result.Failure(new Error(
                Code: "Organization.InsufficientPermissions",
                Message: "Only Owners and Admins can change organization member roles.",
                Type: ErrorType.Forbidden));
        }

        var member = await organizationRepository.GetMemberByIdAsync(
            command.MemberId,
            command.OrganizationId,
            cancellationToken);

        if (member is null)
        {
            return Result.Failure(new Error(
                Code: "Organization.MemberNotFound",
                Message: "Organization member was not found.",
                Type: ErrorType.NotFound));
        }

        if (member.UserId == command.ActorUserId)
        {
            return Result.Failure(new Error(
                Code: "Organization.CannotChangeOwnRole",
                Message: "Members cannot change their own role.",
                Type: ErrorType.Forbidden));
        }

        if ((command.NewRole == MembershipRole.Owner || member.Role == MembershipRole.Owner) &&
            command.ActorRole != MembershipRole.Owner)
        {
            return Result.Failure(new Error(
                Code: "Organization.OwnerRoleRestricted",
                Message: "Only an Owner can grant or revoke the Owner role.",
                Type: ErrorType.Forbidden));
        }

        if (command.ActorRole == MembershipRole.Admin &&
            member.Role is MembershipRole.Owner or MembershipRole.Admin)
        {
            return Result.Failure(new Error(
                Code: "Organization.AdminCannotModifyPeerOrOwner",
                Message: "Admins cannot modify Owners or other Admins.",
                Type: ErrorType.Forbidden));
        }

        if (member.Role == MembershipRole.Owner &&
            command.NewRole != MembershipRole.Owner &&
            member.Status == MembershipStatus.Active)
        {
            var activeOwners = await organizationRepository.CountActiveOwnersAsync(command.OrganizationId, cancellationToken);
            if (activeOwners <= 1)
            {
                return Result.Failure(new Error(
                    Code: "Organization.LastOwnerProtected",
                    Message: "Cannot change role of the last active Owner in the organization.",
                    Type: ErrorType.Validation));
            }
        }

        member.ChangeRole(command.NewRole, clock.UtcNow);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
