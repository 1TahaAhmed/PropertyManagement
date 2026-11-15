using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.Update;

public sealed record UpdateOrganizationCommand(
    Guid OrganizationId,
    string Name,
    string? DefaultCurrency = null) : IRequest<Result<OrganizationResponse>>;
