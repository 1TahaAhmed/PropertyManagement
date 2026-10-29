namespace PropertyManagement.Api.Contracts.Authentication;

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid? OrganizationId = null);
