using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Domain.Entities.Billing;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Billing;

public sealed class ChargeRepository(ApplicationDbContext context) : IChargeRepository
{
    public async Task<Charge?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Charges
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Charge>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Charges
            .Where(c => c.OrganizationId == organizationId && ids.Contains(c.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Charge>> GetPendingByLeaseAsync(Guid leaseId, Guid organizationId, DateOnly? upToPeriodEnd = null, CancellationToken cancellationToken = default)
    {
        var query = context.Charges
            .Where(c => c.OrganizationId == organizationId &&
                        c.LeaseId == leaseId &&
                        c.Status == ChargeStatus.Pending);

        if (upToPeriodEnd.HasValue)
        {
            query = query.Where(c => !c.PeriodStart.HasValue || c.PeriodStart.Value <= upToPeriodEnd.Value);
        }

        return await query
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Charge>> GetByInvoiceIdAsync(Guid invoiceId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Charges
            .Where(c => c.OrganizationId == organizationId && c.InvoiceId == invoiceId)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Charge charge, CancellationToken cancellationToken = default)
    {
        await context.Charges.AddAsync(charge, cancellationToken);
    }

    public void Detach(Charge charge)
    {
        var entry = context.Entry(charge);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    public void SetRowVersion(Charge charge, byte[] rowVersion)
    {
        context.Entry(charge).Property(c => c.RowVersion).OriginalValue = rowVersion;
    }

    public void Update(Charge charge)
    {
        if (context.Entry(charge).State == EntityState.Detached)
        {
            context.Charges.Update(charge);
        }
    }
}
