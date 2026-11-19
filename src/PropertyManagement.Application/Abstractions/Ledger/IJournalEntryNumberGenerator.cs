namespace PropertyManagement.Application.Abstractions.Ledger;

public interface IJournalEntryNumberGenerator
{
    Task<string> GenerateNextAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken = default);
}
