using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed class DeactivateMemberCommandHandler(
    IOrganizationRepository organizationRepository,
    IClock clock) : IRequestHandler<DeactivateMemberCommand, Result>
{
    public async Task<Result> Handle(
        DeactivateMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ActorRole is not (MembershipRole.Owner or MembershipRole.Admin))
        {
            return Result.Failure(new Error(
                Code: "Organization.InsufficientPermissions",
                Message: "Only Owners and Admins can deactivate organization members.",
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
                Code: "Organization.CannotDeactivateSelf",
                Message: "Members cannot deactivate themselves.",
                Type: ErrorType.Forbidden));
        }

        if (member.Role == MembershipRole.Owner &&
            command.ActorRole != MembershipRole.Owner)
        {
            return Result.Failure(new Error(
                Code: "Organization.OwnerRoleRestricted",
                Message: "Only an Owner can deactivate an Owner.",
                Type: ErrorType.Forbidden));
        }

        if (command.ActorRole == MembershipRole.Admin &&
            member.Role is MembershipRole.Owner or MembershipRole.Admin)
        {
            return Result.Failure(new Error(
                Code: "Organization.AdminCannotModifyPeerOrOwner",
                Message: "Admins cannot deactivate Owners or other Admins.",
                Type: ErrorType.Forbidden));
        }

        if (member.Role == MembershipRole.Owner && member.Status == MembershipStatus.Active)
        {
            var activeOwners = await organizationRepository.CountActiveOwnersAsync(command.OrganizationId, cancellationToken);
            if (activeOwners <= 1)
            {
                return Result.Failure(new Error(
                    Code: "Organization.LastOwnerProtected",
                    Message: "Cannot deactivate the last active Owner in the organization.",
                    Type: ErrorType.Validation));
            }
        }

        member.Deactivate(clock.UtcNow);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
