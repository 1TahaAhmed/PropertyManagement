using MediatR;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.GetById;

public sealed class GetOrganizationByIdQueryHandler(
    IOrganizationRepository organizationRepository)
    : IRequestHandler<GetOrganizationByIdQuery, Result<OrganizationResponse>>
{
    public async Task<Result<OrganizationResponse>> Handle(
        GetOrganizationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(query.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.NotFound",
                Message: "Organization was not found.",
                Type: ErrorType.NotFound));
        }

        var member = await organizationRepository.GetMemberAsync(query.OrganizationId, query.UserId, cancellationToken);
        if (member is null || member.Status != MembershipStatus.Active)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.Forbidden",
                Message: "You do not have access to this organization.",
                Type: ErrorType.Forbidden));
        }

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            Id: organization.Id,
            Name: organization.Name,
            Identifier: organization.Identifier,
            Status: organization.Status,
            CreatedAt: organization.CreatedAt,
            UpdatedAt: organization.UpdatedAt));
    }
}
