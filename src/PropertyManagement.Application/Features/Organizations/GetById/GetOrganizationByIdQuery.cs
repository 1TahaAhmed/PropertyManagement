using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.GetById;

public sealed record GetOrganizationByIdQuery(
    Guid OrganizationId,
    Guid UserId) : IRequest<Result<OrganizationResponse>>;
