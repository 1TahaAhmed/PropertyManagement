using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Properties;

public interface IUnitRepository
{
    Task<Unit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNumberAsync(Guid tenantId, Guid propertyId, string unitNumber, CancellationToken cancellationToken = default);
    Task AddAsync(Unit unit, CancellationToken cancellationToken = default);
    void SetRowVersion(Unit unit, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Unit> Items, int TotalCount)> GetPagedByPropertyAsync(
        Guid propertyId,
        UnitOperationalStatus? status,
        UnitType? type,
        decimal? minRent,
        decimal? maxRent,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}