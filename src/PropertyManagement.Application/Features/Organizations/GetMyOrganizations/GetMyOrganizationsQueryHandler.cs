using MediatR;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetMyOrganizations;

public sealed class GetMyOrganizationsQueryHandler(
    IOrganizationRepository organizationRepository)
    : IRequestHandler<GetMyOrganizationsQuery, Result<IReadOnlyList<UserOrganizationResponse>>>
{
    public async Task<Result<IReadOnlyList<UserOrganizationResponse>>> Handle(
        GetMyOrganizationsQuery query,
        CancellationToken cancellationToken)
    {
        var userOrganizations = await organizationRepository.GetUserOrganizationsAsync(
            query.UserId,
            cancellationToken);

        var response = userOrganizations
            .Select(item => new UserOrganizationResponse(
                OrganizationId: item.Organization.Id,
                Name: item.Organization.Name,
                Identifier: item.Organization.Identifier,
                Status: item.Organization.Status,
                Roles: item.Roles,
                IsCurrent: query.CurrentOrganizationId.HasValue
                    && item.Organization.Id == query.CurrentOrganizationId.Value))
            .ToList();

        return Result<IReadOnlyList<UserOrganizationResponse>>.Success(response);
    }
}
