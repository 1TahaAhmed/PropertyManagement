using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Domain.Entities.Ledger;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Ledger;

public sealed class LedgerRepository(ApplicationDbContext context) : ILedgerRepository
{
    public async Task<JournalEntry?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == id && j.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<JournalEntry?> GetByIdempotencyKeyAsync(
        Guid organizationId,
        string sourceType,
        Guid sourceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await context.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(
                j => j.OrganizationId == organizationId &&
                     j.SourceType == sourceType.Trim() &&
                     j.SourceId == sourceId &&
                     j.IdempotencyKey == idempotencyKey.Trim(),
                cancellationToken);
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(
        Guid organizationId,
        string sourceType,
        Guid sourceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await context.JournalEntries.AnyAsync(
            j => j.OrganizationId == organizationId &&
                 j.SourceType == sourceType.Trim() &&
                 j.SourceId == sourceId &&
                 j.IdempotencyKey == idempotencyKey.Trim(),
            cancellationToken);
    }

    public async Task<(IReadOnlyList<JournalEntry> Items, int TotalCount)> GetPagedAsync(
        LedgerFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.OrganizationId == filter.OrganizationId);

        if (!string.IsNullOrWhiteSpace(filter.SourceType))
        {
            var src = filter.SourceType.Trim();
            query = query.Where(j => j.SourceType == src);
        }

        if (filter.SourceId.HasValue)
        {
            query = query.Where(j => j.SourceId == filter.SourceId.Value);
        }

        if (filter.AccountCode.HasValue)
        {
            query = query.Where(j => j.Lines.Any(l => l.AccountCode == filter.AccountCode.Value));
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(j => j.EntryDateUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(j => j.EntryDateUtc <= filter.ToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(j => j.EntryDateUtc)
            .ThenByDescending(j => j.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<AccountBalanceSummary>> GetAccountBalancesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.JournalLines
            .AsNoTracking()
            .Where(l => l.OrganizationId == organizationId)
            .GroupBy(l => new { l.AccountCode, l.AccountType, l.Currency })
            .Select(g => new
            {
                g.Key.AccountCode,
                g.Key.AccountType,
                g.Key.Currency,
                TotalDebits = g.Sum(x => x.Debit),
                TotalCredits = g.Sum(x => x.Credit)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r =>
            {
                var net = r.AccountType is LedgerAccountType.Asset or LedgerAccountType.Expense
                    ? r.TotalDebits - r.TotalCredits
                    : r.TotalCredits - r.TotalDebits;
                return new AccountBalanceSummary(
                    r.AccountCode,
                    r.AccountType,
                    r.Currency,
                    r.TotalDebits,
                    r.TotalCredits,
                    net);
            })
            .OrderBy(r => r.AccountCode)
            .ThenBy(r => r.Currency)
            .ToList();
    }

    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        await context.JournalEntries.AddAsync(entry, cancellationToken);
    }
}
