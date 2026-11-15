using PropertyManagement.Domain.Common;

namespace PropertyManagement.Api.Contracts.Organizations;

public sealed record CreateOrganizationRequest(
    string Name,
    string? Identifier = null,
    string DefaultCurrency = CurrencyCode.Default);
