namespace PropertyManagement.Api.Contracts.Tenants;

public sealed record UpdateTenantRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string NationalIdOrPassport = "",
    string EmergencyContactName = "",
    string EmergencyContactPhone = "",
    string Notes = "",
    string? RowVersion = null);
