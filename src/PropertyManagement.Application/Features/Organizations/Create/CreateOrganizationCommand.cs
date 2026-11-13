using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.Create;

public sealed record CreateOrganizationCommand(
    Guid UserId,
    string Name,
    string? CustomSlug = null) : IRequest<Result<OrganizationResponse>>;
