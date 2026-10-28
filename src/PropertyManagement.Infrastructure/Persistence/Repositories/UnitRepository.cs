using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class UnitRepository(ApplicationDbContext dbContext) : IUnitRepository
{
    public Task<Unit?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return dbContext.Units
            .SingleOrDefaultAsync(u => u.OrganizationId == id && u.TenantId == tenantId, cancellationToken);
    }

    public Task<Unit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Units
            .SingleOrDefaultAsync(u => u.OrganizationId == id, cancellationToken);
    }

    public Task<bool> ExistsByNumberAsync(Guid tenantId, Guid propertyId, string unitNumber, CancellationToken cancellationToken = default)
    {
        return dbContext.Units
            .AnyAsync(u => u.TenantId == tenantId && u.PropertyId == propertyId && u.UnitNumber == unitNumber, cancellationToken);
    }

    public async Task AddAsync(Unit unit, CancellationToken cancellationToken = default)
    {
        await dbContext.Units.AddAsync(unit, cancellationToken);
    }

    public void SetRowVersion(Unit unit, byte[] rowVersion)
    {
        dbContext.Entry(unit).Property(u => u.RowVersion).OriginalValue = rowVersion;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Unit> Items, int TotalCount)> GetPagedByPropertyAsync(
        Guid tenantId,
        Guid propertyId,
        UnitOperationalStatus? status,
        UnitType? type,
        decimal? minRent,
        decimal? maxRent,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Units
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.PropertyId == propertyId);

        if (status.HasValue)
        {
            query = query.Where(u => u.Status == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(u => u.Type == type.Value);
        }

        if (minRent.HasValue)
        {
            query = query.Where(u => u.MarketRent >= minRent.Value);
        }

        if (maxRent.HasValue)
        {
            query = query.Where(u => u.MarketRent <= maxRent.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = pageNumber <= 0 ? 1 : pageNumber;
        var size = pageSize is <= 0 or > 100 ? 10 : pageSize;

        var items = await query
            .OrderBy(u => u.UnitNumber)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}