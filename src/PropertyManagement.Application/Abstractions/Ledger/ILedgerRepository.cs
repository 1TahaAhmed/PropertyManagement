using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Ledger;

public sealed record LedgerFilter(
    Guid OrganizationId,
    string? SourceType = null,
    Guid? SourceId = null,
    LedgerAccountCode? AccountCode = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record AccountBalanceSummary(
    LedgerAccountCode AccountCode,
    LedgerAccountType AccountType,
    string Currency,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal NetBalance);

public interface ILedgerRepository
{
    Task<JournalEntry?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<JournalEntry?> GetByIdempotencyKeyAsync(
        Guid organizationId,
        string sourceType,
        Guid sourceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdempotencyKeyAsync(
        Guid organizationId,
        string sourceType,
        Guid sourceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPagedAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountBalanceSummary>> GetAccountBalancesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);
}
