using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;

namespace PropertyManagement.Application.Features.Leases.GetById;

public sealed record GetLeaseByIdQuery(
    Guid OrganizationId,
    Guid LeaseId) : IRequest<Result<LeaseResponse>>;
