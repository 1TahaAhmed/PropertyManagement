using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetMyOrganizations;

public sealed record GetMyOrganizationsQuery(
    Guid UserId,
    Guid? CurrentOrganizationId = null) : IRequest<Result<IReadOnlyList<UserOrganizationResponse>>>;
