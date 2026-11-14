namespace PropertyManagement.Api.Contracts.Leases;

public sealed record LeaseConcurrencyRequest(string? RowVersion = null);
