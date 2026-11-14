namespace PropertyManagement.Api.Contracts.Leases;

public sealed record TerminateLeaseRequest(
    DateOnly TerminationDate,
    string Reason,
    string? Notes = null,
    string? RowVersion = null);
