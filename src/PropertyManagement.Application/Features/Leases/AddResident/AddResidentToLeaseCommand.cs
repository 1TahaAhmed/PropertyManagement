using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Leases.AddResident;

public sealed record AddResidentToLeaseCommand(
    Guid OrganizationId,
    Guid LeaseId,
    Guid ResidentId,
    LeaseResidentRole Role) : IRequest<Result<LeaseResponse>>;
