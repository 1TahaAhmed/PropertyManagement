namespace PropertyManagement.Api.Contracts.Residents;

public sealed record UpdateResidentRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? MiddleName = null,
    string? PreferredName = null,
    string? Email = null,
    string? AlternatePhone = null,
    string? RowVersion = null,
    string? NationalIdOrPassport = null,
    string? Notes = null);
