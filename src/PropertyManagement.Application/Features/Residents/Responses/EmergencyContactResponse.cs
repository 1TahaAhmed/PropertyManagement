namespace PropertyManagement.Application.Features.Residents.Responses;

public sealed record EmergencyContactResponse(
    Guid Id,
    Guid ResidentId,
    string Name,
    string Relationship,
    string Phone,
    string? Email,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
