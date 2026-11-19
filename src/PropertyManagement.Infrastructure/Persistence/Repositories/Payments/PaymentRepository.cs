using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Payments;

public sealed class PaymentRepository(ApplicationDbContext context) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .Include(p => p.Allocations)
            .Include(p => p.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<Payment?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Allocations)
            .Include(p => p.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var normalized = idempotencyKey.Trim();
        return await context.Payments
            .Include(p => p.Allocations)
            .Include(p => p.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.IdempotencyKey == normalized, cancellationToken);
    }

    public async Task<Payment?> GetByProviderPaymentIdAsync(PaymentProviderType provider, string providerPaymentId, CancellationToken cancellationToken = default)
    {
        var normalized = providerPaymentId.Trim();
        return await context.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Allocations)
            .Include(p => p.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderPaymentId == normalized, cancellationToken);
    }

    public async Task<Payment?> GetByProviderTransactionIdAsync(PaymentProviderType provider, string providerTransactionId, CancellationToken cancellationToken = default)
    {
        var normalized = providerTransactionId.Trim();
        return await context.Payments
            .IgnoreQueryFilters()
            .Include(p => p.Allocations)
            .Include(p => p.Refunds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderTransactionId == normalized, cancellationToken);
    }

    public async Task<(IReadOnlyList<Payment> Items, int TotalCount)> GetPagedAsync(PaymentFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> query = context.Payments;
        if (filter.IncludeDetails)
        {
            query = query
                .Include(p => p.Allocations)
                .Include(p => p.Refunds)
                .AsSplitQuery();
        }

        if (filter.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        query = query.Where(p => p.OrganizationId == filter.OrganizationId);

        if (filter.ResidentId.HasValue)
        {
            query = query.Where(p => p.ResidentId == filter.ResidentId.Value);
        }

        if (filter.LeaseId.HasValue)
        {
            query = query.Where(p => p.LeaseId == filter.LeaseId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.PaymentMethod.HasValue)
        {
            query = query.Where(p => p.PaymentMethod == filter.PaymentMethod.Value);
        }

        if (filter.Provider.HasValue)
        {
            query = query.Where(p => p.Provider == filter.Provider.Value);
        }

        if (filter.CreatedFromUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= filter.CreatedFromUtc.Value);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= filter.CreatedToUtc.Value);
        }

        if (filter.OnlyUnapplied)
        {
            query = query.Where(p => p.UnappliedAmount > 0m);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Payment>> GetByProviderAndWindowAsync(
        Guid organizationId,
        PaymentProviderType provider,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId &&
                        p.Provider == provider &&
                        (p.SettledAtUtc ?? p.ReceivedAtUtc ?? p.CreatedAtUtc) >= fromUtc &&
                        (p.SettledAtUtc ?? p.ReceivedAtUtc ?? p.CreatedAtUtc) <= toUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AcquirePaymentLockAsync(Guid paymentId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (context.Database.IsSqlServer())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM [Payments] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {paymentId} AND [OrganizationId] = {organizationId}",
                cancellationToken);
        }
    }

    public async Task AcquireResidentLockAsync(Guid residentId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (context.Database.IsSqlServer())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM [Residents] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {residentId} AND [OrganizationId] = {organizationId}",
                cancellationToken);
        }
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await context.Payments.AddAsync(payment, cancellationToken);
    }

    public void SetRowVersion(Payment payment, byte[] rowVersion)
    {
        context.Entry(payment).Property(p => p.RowVersion).OriginalValue = rowVersion;
    }

    public void Detach(Payment payment)
    {
        foreach (var allocation in payment.Allocations)
        {
            context.Entry(allocation).State = EntityState.Detached;
        }

        foreach (var refund in payment.Refunds)
        {
            context.Entry(refund).State = EntityState.Detached;
        }

        context.Entry(payment).State = EntityState.Detached;
    }
}
