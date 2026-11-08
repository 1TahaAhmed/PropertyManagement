using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed record GetEmergencyContactsQuery(
    Guid ResidentId,
    Guid OrganizationId) : IRequest<Result<IReadOnlyList<EmergencyContactResponse>>>;
