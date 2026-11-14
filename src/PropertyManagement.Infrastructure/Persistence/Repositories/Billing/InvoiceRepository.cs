using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Billing;

public sealed class InvoiceRepository(ApplicationDbContext context) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Adjustments)
            .FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var normalized = invoiceNumber.Trim();
        return await context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Adjustments)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == normalized && i.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<Invoice?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var normalized = idempotencyKey.Trim();
        return await context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Adjustments)
            .FirstOrDefaultAsync(
                i => i.OrganizationId == organizationId &&
                     i.IdempotencyKey == normalized &&
                     i.Status != InvoiceStatus.Void,
                cancellationToken);
    }

    public async Task<bool> ExistsActiveByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var normalized = idempotencyKey.Trim();
        return await context.Invoices.AnyAsync(
            i => i.OrganizationId == organizationId &&
                 i.IdempotencyKey == normalized &&
                 i.Status != InvoiceStatus.Void,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByLeaseIdAsync(Guid leaseId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Adjustments)
            .Where(i => i.LeaseId == leaseId && i.OrganizationId == organizationId)
            .OrderByDescending(i => i.BillingPeriodStart)
            .ThenByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> GetPagedAsync(InvoiceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Adjustments)
            .Where(i => i.OrganizationId == filter.OrganizationId);

        if (filter.LeaseId.HasValue)
        {
            query = query.Where(i => i.LeaseId == filter.LeaseId.Value);
        }

        if (filter.ResidentId.HasValue)
        {
            query = query.Where(i => i.ResidentId == filter.ResidentId.Value);
        }

        if (filter.PropertyId.HasValue)
        {
            query = query.Where(i => i.PropertyId == filter.PropertyId.Value);
        }

        if (filter.UnitId.HasValue)
        {
            query = query.Where(i => i.UnitId == filter.UnitId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }

        if (filter.OnlyOverdue)
        {
            query = query.Where(i => i.Status == InvoiceStatus.Overdue);
        }

        if (filter.DueBefore.HasValue)
        {
            query = query.Where(i => i.DueDate <= filter.DueBefore.Value && i.BalanceDue > 0m && i.Status != InvoiceStatus.Void);
        }

        if (filter.PeriodStart.HasValue)
        {
            query = query.Where(i => i.BillingPeriodEnd >= filter.PeriodStart.Value);
        }

        if (filter.PeriodEnd.HasValue)
        {
            query = query.Where(i => i.BillingPeriodStart <= filter.PeriodEnd.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
        {
            var term = filter.InvoiceNumber.Trim();
            query = query.Where(i => i.InvoiceNumber != null && i.InvoiceNumber.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await context.Invoices.AddAsync(invoice, cancellationToken);
    }

    public void SetRowVersion(Invoice invoice, byte[] rowVersion)
    {
        context.Entry(invoice).Property(i => i.RowVersion).OriginalValue = rowVersion;
    }

    public void Update(Invoice invoice)
    {
        if (context.Entry(invoice).State == EntityState.Detached)
        {
            context.Invoices.Update(invoice);
        }
    }
}
