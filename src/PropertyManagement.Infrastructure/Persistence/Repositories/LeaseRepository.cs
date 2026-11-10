using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class LeaseRepository(ApplicationDbContext context) : ILeaseRepository
{
    public async Task<Lease?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.Leases
            .Include(l => l.Residents)
            .FirstOrDefaultAsync(l => l.Id == id && l.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<bool> HasOverlappingLeaseAsync(
        Guid unitId,
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        // Cancelled leases are excluded from overlap checks.
        // Active and PendingApproval leases block overlapping bookings.
        // Overlap formula: max(StartA, StartB) <= min(EndA, EndB)
        // Which is equivalent to: StartA <= EndB && EndA >= StartB
        var query = context.Leases
            .Where(l => l.UnitId == unitId
                     && l.OrganizationId == organizationId
                     && l.Status != LeaseStatus.Cancelled);

        if (excludingLeaseId.HasValue)
        {
            query = query.Where(l => l.Id != excludingLeaseId.Value);
        }

        var leases = await query.ToListAsync(cancellationToken);

        // Perform precise domain overlap check (accounting for Terminated actual dates)
        return leases.Any(l => l.OverlapsWith(startDate, endDate));
    }

    public async Task<IReadOnlyList<Lease>> GetByUnitIdAsync(
        Guid unitId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await context.Leases
            .Include(l => l.Residents)
            .Where(l => l.UnitId == unitId && l.OrganizationId == organizationId)
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Lease?> GetActiveLeaseForUnitAsync(
        Guid unitId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await context.Leases
            .Include(l => l.Residents)
            .FirstOrDefaultAsync(l => l.UnitId == unitId
                                   && l.OrganizationId == organizationId
                                   && l.Status == LeaseStatus.Active, cancellationToken);
    }

    public async Task AddAsync(Lease lease, CancellationToken cancellationToken = default)
    {
        await context.Leases.AddAsync(lease, cancellationToken);
    }

    public void Update(Lease lease)
    {
        context.Leases.Update(lease);
    }
}
