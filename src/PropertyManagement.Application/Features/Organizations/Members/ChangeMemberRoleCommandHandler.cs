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
