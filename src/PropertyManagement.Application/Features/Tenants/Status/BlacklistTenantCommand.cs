using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Tenants.Status;

public sealed record BlacklistTenantCommand(
    Guid TenantId,
    Guid OrganizationId,
    string Reason = "") : IRequest<Result>;
