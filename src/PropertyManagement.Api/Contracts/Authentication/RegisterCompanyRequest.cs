namespace PropertyManagement.Api.Contracts.Authentication;

public sealed record RegisterCompanyRequest(
    string CompanyName,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Address
);