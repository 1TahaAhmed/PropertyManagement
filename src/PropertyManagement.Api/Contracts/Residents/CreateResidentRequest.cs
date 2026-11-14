namespace PropertyManagement.Api.Contracts.Residents;

public sealed record CreateResidentRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? MiddleName = null,
    string? PreferredName = null,
    string? Email = null,
    string? AlternatePhone = null,
    string? NationalIdOrPassport = null,
    string? Notes = null);
