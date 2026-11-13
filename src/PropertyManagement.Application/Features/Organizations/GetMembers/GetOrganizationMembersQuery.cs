using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetMembers;

public sealed record GetOrganizationMembersQuery(Guid OrganizationId)
    : IRequest<Result<IReadOnlyList<OrganizationMemberResponse>>>;
