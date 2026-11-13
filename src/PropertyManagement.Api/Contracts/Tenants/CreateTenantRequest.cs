namespace PropertyManagement.Api.Contracts.Tenants;

public sealed record CreateTenantRequest(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string NationalIdOrPassport = "",
    string EmergencyContactName = "",
    string EmergencyContactPhone = "",
    string Notes = "");
