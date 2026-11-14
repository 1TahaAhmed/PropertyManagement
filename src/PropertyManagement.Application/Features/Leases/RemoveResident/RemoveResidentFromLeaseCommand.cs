using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.RemoveResident;

public sealed record RemoveResidentFromLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId,
    Guid ResidentId) : IRequest<Result<LeaseResponse>>;
