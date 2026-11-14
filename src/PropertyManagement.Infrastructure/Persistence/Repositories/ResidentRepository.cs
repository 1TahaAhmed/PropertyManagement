using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories;

public sealed class ResidentRepository(ApplicationDbContext dbContext) : IResidentRepository
{
    public Task<Resident?> GetByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Residents
            .SingleOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId, cancellationToken);
    }

    public Task<Resident?> GetByIdWithContactsAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Residents
            .Include(r => r.EmergencyContacts)
            .SingleOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Resident> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        ResidentStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        LeaseResidentRole? leaseRole = null,
        bool onlyLeaseHolders = false)
    {
        var query = dbContext.Residents
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var trimmedSearch = searchTerm.Trim();
            query = query.Where(r =>
                r.FirstName.Contains(trimmedSearch) ||
                r.LastName.Contains(trimmedSearch) ||
                r.Phone.Contains(trimmedSearch) ||
                r.NationalIdOrPassport.Contains(trimmedSearch) ||
                (r.Email != null && r.Email.Contains(trimmedSearch)));
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (onlyLeaseHolders)
        {
            query = query.Where(r => dbContext.LeaseResidents.Any(lr =>
                lr.OrganizationId == organizationId &&
                lr.ResidentId == r.Id &&
                (lr.Role == LeaseResidentRole.PrimaryTenant || lr.Role == LeaseResidentRole.CoTenant)));
        }

        if (leaseRole.HasValue)
        {
            query = query.Where(r => dbContext.LeaseResidents.Any(lr =>
                lr.OrganizationId == organizationId &&
                lr.ResidentId == r.Id &&
                lr.Role == leaseRole.Value));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = pageNumber <= 0 ? 1 : pageNumber;
        var size = pageSize is <= 0 or > 100 ? 10 : pageSize;

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> IsLeaseHolderAsync(
        Guid residentId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.LeaseResidents.AnyAsync(
            lr => lr.OrganizationId == organizationId &&
                  lr.ResidentId == residentId &&
                  (lr.Role == LeaseResidentRole.PrimaryTenant || lr.Role == LeaseResidentRole.CoTenant),
            cancellationToken);
    }

    public async Task AddAsync(Resident resident, CancellationToken cancellationToken = default)
    {
        await dbContext.Residents.AddAsync(resident, cancellationToken);
    }

    public void SetRowVersion(Resident resident, byte[] rowVersion)
    {
        dbContext.Entry(resident).Property(r => r.RowVersion).OriginalValue = rowVersion;
    }

    public void RemoveEmergencyContact(EmergencyContact contact)
    {
        dbContext.EmergencyContacts.Remove(contact);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
