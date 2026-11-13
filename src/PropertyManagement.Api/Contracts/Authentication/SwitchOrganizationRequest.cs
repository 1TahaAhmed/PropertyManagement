namespace PropertyManagement.Api.Contracts.Authentication;

public sealed record SwitchOrganizationRequest(
    Guid OrganizationId,
    string? RefreshToken = null);
