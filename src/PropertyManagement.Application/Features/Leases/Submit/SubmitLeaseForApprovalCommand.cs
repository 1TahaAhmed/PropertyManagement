using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.Submit;

public sealed record SubmitLeaseForApprovalCommand(
    Guid OrganizationId,
    Guid LeaseId,
    string? RowVersion = null) : IRequest<Result<LeaseResponse>>;
