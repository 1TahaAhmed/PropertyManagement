using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.Approve;

public sealed record ApproveLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId) : IRequest<Result<LeaseResponse>>;
