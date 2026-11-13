using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Tenants.Status;

public sealed record DeactivateTenantCommand(
    Guid TenantId,
    Guid OrganizationId) : IRequest<Result>;
