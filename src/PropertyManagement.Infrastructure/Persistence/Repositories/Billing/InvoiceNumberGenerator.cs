using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Infrastructure.Persistence.Entities;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Billing;

public sealed class InvoiceNumberGenerator(ApplicationDbContext context) : IInvoiceNumberGenerator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> InMemoryLocks = new();

    public async Task<string> GenerateNextAsync(Guid organizationId, int year, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        }

        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Invoice sequence year must be between 2000 and 9999.");
        }

        long allocatedNumber;

        if (context.Database.IsSqlServer())
        {
            allocatedNumber = await AllocateSqlServerAsync(organizationId, year, cancellationToken);
        }
        else if (string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            allocatedNumber = await AllocateSqliteAsync(organizationId, year, cancellationToken);
        }
        else
        {
            allocatedNumber = await AllocateInMemoryAsync(organizationId, year, cancellationToken);
        }

        return $"INV-{year:0000}-{allocatedNumber:D6}";
    }

    private async Task<long> AllocateSqlServerAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE INTO [InvoiceNumberSequences] WITH (HOLDLOCK) AS target
            USING (SELECT @orgId AS [OrganizationId], @year AS [Year]) AS source
                ON target.[OrganizationId] = source.[OrganizationId] AND target.[Year] = source.[Year]
            WHEN MATCHED THEN
                UPDATE SET [NextValue] = target.[NextValue] + 1
            WHEN NOT MATCHED THEN
                INSERT ([OrganizationId], [Year], [NextValue]) VALUES (@orgId, @year, 2)
            OUTPUT CASE WHEN $action = 'INSERT' THEN CAST(1 AS BIGINT) ELSE deleted.[NextValue] END;
            """;

        return await ExecuteScalarSequenceAsync(sql, organizationId, year, cancellationToken);
    }

    private async Task<long> AllocateSqliteAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO InvoiceNumberSequences (OrganizationId, Year, NextValue)
            VALUES (@orgId, @year, 2)
            ON CONFLICT(OrganizationId, Year) DO UPDATE SET NextValue = NextValue + 1
            RETURNING NextValue - 1;
            """;

        return await ExecuteScalarSequenceAsync(sql, organizationId, year, cancellationToken);
    }

    private async Task<long> ExecuteScalarSequenceAsync(
        string sql,
        Guid organizationId,
        int year,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            var orgParam = command.CreateParameter();
            orgParam.ParameterName = "@orgId";
            orgParam.Value = organizationId;
            command.Parameters.Add(orgParam);

            var yearParam = command.CreateParameter();
            yearParam.ParameterName = "@year";
            yearParam.Value = year;
            command.Parameters.Add(yearParam);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null || scalar == DBNull.Value)
            {
                throw new InvalidOperationException("Failed to allocate next invoice number sequence.");
            }

            return Convert.ToInt64(scalar);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<long> AllocateInMemoryAsync(Guid organizationId, int year, CancellationToken cancellationToken)
    {
        var key = $"{organizationId:N}:{year}";
        var gate = InMemoryLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            var existingSequence = await context.InvoiceNumberSequences
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Year == year, cancellationToken);

            var dbNext = existingSequence?.NextValue ?? 1L;

            // Also inspect already-persisted invoices in this InMemory store so isolated test databases never drift
            var prefix = $"INV-{year:0000}-";
            var existingNumbers = await context.Invoices
                .AsNoTracking()
                .Where(i => i.OrganizationId == organizationId && i.InvoiceNumber != null && i.InvoiceNumber.StartsWith(prefix))
                .Select(i => i.InvoiceNumber!)
                .ToListAsync(cancellationToken);

            var maxUsedInDb = 0L;
            foreach (var number in existingNumbers)
            {
                if (long.TryParse(number.AsSpan(prefix.Length), out var parsed) && parsed > maxUsedInDb)
                {
                    maxUsedInDb = parsed;
                }
            }

            var current = Math.Max(dbNext, maxUsedInDb + 1L);
            var nextAfter = current + 1L;

            if (existingSequence is null)
            {
                await context.InvoiceNumberSequences.AddAsync(
                    new InvoiceNumberSequence(organizationId, year, nextAfter),
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
