using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.ChangeResidentRole;

public sealed record ChangeLeaseResidentRoleCommand(
    Guid OrganizationId,
    Guid LeaseId,
    Guid ResidentId,
    LeaseResidentRole NewRole) : IRequest<Result<LeaseResponse>>;
