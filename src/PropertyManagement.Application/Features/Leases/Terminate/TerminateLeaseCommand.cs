using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.Terminate;

public sealed record TerminateLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId,
    DateOnly TerminationDate,
    string Reason,
    Guid TerminatedByUserId,
    string? Notes = null,
    string? RowVersion = null) : IRequest<Result<LeaseResponse>>;
