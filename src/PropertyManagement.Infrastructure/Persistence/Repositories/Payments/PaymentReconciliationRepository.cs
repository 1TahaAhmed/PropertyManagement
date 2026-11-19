using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Payments;

public sealed class PaymentReconciliationRepository(ApplicationDbContext context) : IPaymentReconciliationRepository
{
    public async Task<PaymentReconciliationRecord?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.PaymentReconciliationRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<(IReadOnlyList<PaymentReconciliationRecord> Items, int TotalCount)> GetPagedAsync(
        ReconciliationFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = context.PaymentReconciliationRecords
            .AsNoTracking()
            .Where(r => r.OrganizationId == filter.OrganizationId);

        if (filter.BatchId.HasValue)
        {
            query = query.Where(r => r.BatchId == filter.BatchId.Value);
        }

        if (filter.Provider.HasValue)
        {
            query = query.Where(r => r.Provider == filter.Provider.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddRangeAsync(IEnumerable<PaymentReconciliationRecord> records, CancellationToken cancellationToken = default)
    {
        await context.PaymentReconciliationRecords.AddRangeAsync(records, cancellationToken);
    }
}
