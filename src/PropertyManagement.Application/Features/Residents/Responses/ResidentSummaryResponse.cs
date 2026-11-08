using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.Responses;

public sealed record ResidentSummaryResponse(
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
    ResidentStatus Status,
    Guid? UserId,
    int EmergencyContactsCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
