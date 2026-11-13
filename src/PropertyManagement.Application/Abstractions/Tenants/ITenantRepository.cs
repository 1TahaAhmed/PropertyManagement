using PropertyManagement.Domain.Entities.Tenants;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Tenants;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        TenantStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(
        Guid organizationId,
        string email,
        Guid? excludeTenantId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    void SetRowVersion(Tenant tenant, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
