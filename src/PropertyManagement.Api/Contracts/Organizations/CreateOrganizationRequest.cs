namespace PropertyManagement.Api.Contracts.Organizations;

public sealed record CreateOrganizationRequest(
    string Name,
    string? Identifier = null);
