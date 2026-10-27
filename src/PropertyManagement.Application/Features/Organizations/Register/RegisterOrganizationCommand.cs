using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.Register;

public sealed record RegisterOrganizationCommand(
    string Name,
    Guid OwnerUserId) : IRequest<Result<OrganizationResponse>>;