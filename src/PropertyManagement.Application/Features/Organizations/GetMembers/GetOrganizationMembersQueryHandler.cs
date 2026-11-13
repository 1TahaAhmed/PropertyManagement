using MediatR;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetMembers;

public sealed class GetOrganizationMembersQueryHandler(
    IOrganizationRepository organizationRepository,
    IIdentityService identityService)
    : IRequestHandler<GetOrganizationMembersQuery, Result<IReadOnlyList<OrganizationMemberResponse>>>
{
    public async Task<Result<IReadOnlyList<OrganizationMemberResponse>>> Handle(
        GetOrganizationMembersQuery query,
        CancellationToken cancellationToken)
    {
        var members = await organizationRepository.GetMembersAsync(query.OrganizationId, cancellationToken);
        var usersById = await identityService.GetUsersByIdsAsync(
            members.Select(m => m.UserId),
            cancellationToken);

        var response = members
            .Select(m =>
            {
                usersById.TryGetValue(m.UserId, out var user);
                return new OrganizationMemberResponse(
                    Id: m.Id,
                    OrganizationId: m.OrganizationId,
                    UserId: m.UserId,
                    Email: user?.Email,
                    FirstName: user?.FirstName,
                    LastName: user?.LastName,
                    Role: m.Role,
                    Status: m.Status,
                    JoinedAt: m.JoinedAt,
                    UpdatedAt: m.UpdatedAt);
            })
            .ToList();

        return Result<IReadOnlyList<OrganizationMemberResponse>>.Success(response);
    }
}
