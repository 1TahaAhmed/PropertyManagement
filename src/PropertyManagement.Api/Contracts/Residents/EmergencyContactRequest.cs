namespace PropertyManagement.Api.Contracts.Residents;

public sealed record EmergencyContactRequest(
    string Name,
    string Relationship,
    string Phone,
    string? Email = null,
    string? Notes = null);
