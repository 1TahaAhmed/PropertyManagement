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
        var query = context.Leases
            .Where(l => l.UnitId == unitId
                     && l.OrganizationId == organizationId
                     && l.Status != LeaseStatus.Cancelled);

        if (excludingLeaseId.HasValue)
        {
            query = query.Where(l => l.Id != excludingLeaseId.Value);
        }

        var leases = await query.ToListAsync(cancellationToken);

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

    public async Task<(IReadOnlyList<Lease> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        Guid? propertyId,
        Guid? unitId,
        LeaseStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.Leases
            .Include(l => l.Residents)
            .Where(l => l.OrganizationId == organizationId);

        if (propertyId.HasValue)
        {
            query = query.Where(l => l.PropertyId == propertyId.Value);
        }

        if (unitId.HasValue)
        {
            query = query.Where(l => l.UnitId == unitId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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
        if (context.Entry(lease).State == EntityState.Detached)
        {
            context.Leases.Update(lease);
        }
    }
}
