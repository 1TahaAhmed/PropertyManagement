using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(ApplicationDbContext dbContext) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Tenants
            .SingleOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        TenantStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var trimmed = searchTerm.Trim();
            query = query.Where(t =>
                t.FirstName.Contains(trimmed) ||
                t.LastName.Contains(trimmed) ||
                t.Email.Contains(trimmed) ||
                t.PhoneNumber.Contains(trimmed) ||
                t.NationalIdOrPassport.Contains(trimmed));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = pageNumber <= 0 ? 1 : pageNumber;
        var size = pageSize is <= 0 or > 100 ? 10 : pageSize;

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> ExistsByEmailAsync(
        Guid organizationId,
        string email,
        Guid? excludeTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Tenants.AnyAsync(
            t => t.OrganizationId == organizationId
              && t.Email == normalizedEmail
              && (!excludeTenantId.HasValue || t.Id != excludeTenantId.Value),
            cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await dbContext.Tenants.AddAsync(tenant, cancellationToken);
    }

    public void SetRowVersion(Tenant tenant, byte[] rowVersion)
    {
        dbContext.Entry(tenant).Property(t => t.RowVersion).OriginalValue = rowVersion;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
