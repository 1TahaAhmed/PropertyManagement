namespace PropertyManagement.Application.Features.Companies.Register;

public sealed record RegisterCompanyResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid OrganizationId,
    string OrganizationName,
    bool RequiresEmailConfirmation
);