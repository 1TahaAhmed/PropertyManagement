using PropertyManagement.Domain.Entities.Properties;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Properties;

public interface IPropertyRepository
{
    Task<Property?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid organizationId, string name, Guid? excludePropertyId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Property property, CancellationToken cancellationToken = default);
    void SetRowVersion(Property property, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Property> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        PropertyStatus? status,
        PropertyType? type,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
