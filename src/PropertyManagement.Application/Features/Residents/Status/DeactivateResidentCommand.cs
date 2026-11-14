using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.Status;

public sealed record DeactivateResidentCommand(Guid ResidentId, Guid OrganizationId) : IRequest<Result>;
