using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.Status;

public sealed record ActivateResidentCommand(Guid ResidentId, Guid OrganizationId) : IRequest<Result>;
