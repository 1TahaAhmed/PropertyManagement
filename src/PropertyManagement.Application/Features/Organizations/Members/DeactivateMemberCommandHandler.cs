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
