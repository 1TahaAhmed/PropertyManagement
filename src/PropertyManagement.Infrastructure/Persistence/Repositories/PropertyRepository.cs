using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class PropertyRepository(ApplicationDbContext dbContext) : IPropertyRepository
{
    public Task<Property?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return dbContext.Properties
            .SingleOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, cancellationToken);
    }

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Properties
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        Guid organizationId,
        string name,
        Guid? excludePropertyId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Properties
            .AnyAsync(p =>
                p.OrganizationId == organizationId &&
                p.Name == name &&
                (!excludePropertyId.HasValue || p.Id != excludePropertyId.Value),
                cancellationToken);
    }

    public async Task AddAsync(Property property, CancellationToken cancellationToken = default)
    {
        await dbContext.Properties.AddAsync(property, cancellationToken);
    }

    public void SetRowVersion(Property property, byte[] rowVersion)
    {
        dbContext.Entry(property).Property(p => p.RowVersion).OriginalValue = rowVersion;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Property> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        PropertyStatus? status,
        PropertyType? type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Properties
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var trimmedSearch = searchTerm.Trim();
            query = query.Where(p => p.Name.Contains(trimmedSearch) || p.Address.Contains(trimmedSearch));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(p => p.Type == type.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = pageNumber <= 0 ? 1 : pageNumber;
        var size = pageSize is <= 0 or > 100 ? 10 : pageSize;

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
