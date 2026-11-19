using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Infrastructure.Persistence.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Ledger;

public sealed class JournalEntryNumberGenerator(ApplicationDbContext context) : IJournalEntryNumberGenerator
{
    private static readonly ConcurrentDictionary<(Guid OrgId, int Year), SemaphoreSlim> ProcessGates = new();

    public async Task<string> GenerateNextAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var year = utcNow.Year;

        if (context.Database.IsSqlServer())
        {
            if (context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Journal entry number generation on SQL Server requires an active database transaction.");
            }

            var updated = await context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [JournalEntryNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                SET [NextValue] = [NextValue] + 1
                WHERE [OrganizationId] = {organizationId} AND [Year] = {year}");

            if (updated == 0)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($@"
                    IF NOT EXISTS (
                        SELECT 1 FROM [JournalEntryNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [OrganizationId] = {organizationId} AND [Year] = {year}
                    )
                    BEGIN
                        INSERT INTO [JournalEntryNumberSequences] ([OrganizationId], [Year], [NextValue])
                        VALUES ({organizationId}, {year}, 2);
                    END
                    ELSE
                    BEGIN
                        UPDATE [JournalEntryNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                        SET [NextValue] = [NextValue] + 1
                        WHERE [OrganizationId] = {organizationId} AND [Year] = {year};
                    END");
            }

            var sequence = await context.JournalEntryNumberSequences
                .AsNoTracking()
                .FirstAsync(s => s.OrganizationId == organizationId && s.Year == year, cancellationToken);

            var allocated = sequence.NextValue - 1;
            return $"JE-{year:0000}-{allocated:000000}";
        }

        var nextNumber = await AllocateRelationalFallbackAsync(organizationId, year, cancellationToken);
        return $"JE-{year:0000}-{nextNumber:000000}";
    }

    private async Task<long> AllocateRelationalFallbackAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        var gate = ProcessGates.GetOrAdd((organizationId, year), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existingSequence = context.JournalEntryNumberSequences.Local
                .FirstOrDefault(s => s.OrganizationId == organizationId && s.Year == year)
                ?? await context.JournalEntryNumberSequences
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Year == year, cancellationToken);

            var dbNext = existingSequence?.NextValue ?? 1L;
            var prefix = $"JE-{year:0000}-";

            var existingNumbers = await context.JournalEntries
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(j => j.OrganizationId == organizationId && j.EntryNumber.StartsWith(prefix))
                .Select(j => j.EntryNumber)
                .ToListAsync(cancellationToken);

            var maxUsed = 0L;
            foreach (var localNumber in context.JournalEntries.Local
                .Where(j => j.OrganizationId == organizationId && j.EntryNumber.StartsWith(prefix))
                .Select(j => j.EntryNumber))
            {
                if (long.TryParse(localNumber.AsSpan(prefix.Length), out var parsed) && parsed > maxUsed)
                {
                    maxUsed = parsed;
                }
            }
            foreach (var number in existingNumbers)
            {
                if (long.TryParse(number.AsSpan(prefix.Length), out var parsed) && parsed > maxUsed)
                {
                    maxUsed = parsed;
                }
            }

            var current = Math.Max(dbNext, maxUsed + 1L);
            var nextAfter = current + 1L;

            if (existingSequence is null)
            {
                await context.JournalEntryNumberSequences.AddAsync(
                    new JournalEntryNumberSequence(organizationId, year, nextAfter),
                    cancellationToken);
            }
            else
            {
                existingSequence.AdvanceTo(nextAfter);
            }

            return current;
        }
        finally
        {
            gate.Release();
        }
    }
}
