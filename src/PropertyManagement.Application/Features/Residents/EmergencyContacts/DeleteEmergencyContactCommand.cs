using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed record DeleteEmergencyContactCommand(
    Guid ResidentId,
    Guid ContactId,
    Guid OrganizationId) : IRequest<Result>;
