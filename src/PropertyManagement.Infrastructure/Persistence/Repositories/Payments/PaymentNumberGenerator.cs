using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Infrastructure.Persistence.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Payments;

public sealed class PaymentNumberGenerator(ApplicationDbContext context) : IPaymentNumberGenerator
{
    private static readonly ConcurrentDictionary<(Guid OrgId, int Year), SemaphoreSlim> ProcessGates = new();

    public async Task<string> GenerateNextAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var year = utcNow.Year;

        if (context.Database.IsSqlServer())
        {
            if (context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Payment number generation on SQL Server requires an active database transaction.");
            }

            var updated = await context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE [PaymentNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                SET [NextValue] = [NextValue] + 1
                WHERE [OrganizationId] = {organizationId} AND [Year] = {year}");

            long allocated;
            if (updated == 0)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($@"
                    IF NOT EXISTS (
                        SELECT 1 FROM [PaymentNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                        WHERE [OrganizationId] = {organizationId} AND [Year] = {year}
                    )
                    BEGIN
                        INSERT INTO [PaymentNumberSequences] ([OrganizationId], [Year], [NextValue])
                        VALUES ({organizationId}, {year}, 2);
                    END
                    ELSE
                    BEGIN
                        UPDATE [PaymentNumberSequences] WITH (UPDLOCK, HOLDLOCK)
                        SET [NextValue] = [NextValue] + 1
                        WHERE [OrganizationId] = {organizationId} AND [Year] = {year};
                    END");
            }

            var sequence = await context.PaymentNumberSequences
                .AsNoTracking()
                .FirstAsync(s => s.OrganizationId == organizationId && s.Year == year, cancellationToken);

            allocated = sequence.NextValue - 1;
            return $"PAY-{year:0000}-{allocated:000000}";
        }

        var nextNumber = await AllocateRelationalFallbackAsync(organizationId, year, cancellationToken);
        return $"PAY-{year:0000}-{nextNumber:000000}";
    }

    private async Task<long> AllocateRelationalFallbackAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        var gate = ProcessGates.GetOrAdd((organizationId, year), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existingSequence = context.PaymentNumberSequences.Local
                .FirstOrDefault(s => s.OrganizationId == organizationId && s.Year == year)
                ?? await context.PaymentNumberSequences
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Year == year, cancellationToken);

            var dbNext = existingSequence?.NextValue ?? 1L;
            var prefix = $"PAY-{year:0000}-";

            var existingNumbers = await context.Payments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.OrganizationId == organizationId && p.PaymentNumber != null && p.PaymentNumber.StartsWith(prefix))
                .Select(p => p.PaymentNumber!)
                .ToListAsync(cancellationToken);

            var maxUsed = 0L;
            foreach (var localNumber in context.Payments.Local
                .Where(p => p.OrganizationId == organizationId && p.PaymentNumber != null && p.PaymentNumber.StartsWith(prefix))
                .Select(p => p.PaymentNumber!))
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
                await context.PaymentNumberSequences.AddAsync(
                    new PaymentNumberSequence(organizationId, year, nextAfter),
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
