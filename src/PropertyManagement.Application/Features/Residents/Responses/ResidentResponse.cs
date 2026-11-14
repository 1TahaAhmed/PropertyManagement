using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.Responses;

public sealed record ResidentResponse(
    Guid Id,
    Guid OrganizationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PreferredName,
    string FullName,
    string DisplayName,
    string? Email,
    string Phone,
    string? AlternatePhone,
    string NationalIdOrPassport,
    string Notes,
    ResidentStatus Status,
    Guid? UserId,
    IReadOnlyList<EmergencyContactResponse> EmergencyContacts,
    byte[] RowVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
