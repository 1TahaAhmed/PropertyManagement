namespace PropertyManagement.Api.Contracts.Organizations;

public sealed record RegisterOrganizationRequest(
    string Name,
    Guid OwnerUserId);