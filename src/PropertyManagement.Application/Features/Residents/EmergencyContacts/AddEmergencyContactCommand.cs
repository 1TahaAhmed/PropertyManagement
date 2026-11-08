using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed record AddEmergencyContactCommand(
    Guid ResidentId,
    Guid OrganizationId,
    string Name,
    string Relationship,
    string Phone,
    string? Email = null,
    string? Notes = null) : IRequest<Result<EmergencyContactResponse>>;
