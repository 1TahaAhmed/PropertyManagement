using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.Status;

public sealed record BlacklistResidentCommand(
    Guid ResidentId,
    Guid OrganizationId,
    string Reason) : IRequest<Result>;
