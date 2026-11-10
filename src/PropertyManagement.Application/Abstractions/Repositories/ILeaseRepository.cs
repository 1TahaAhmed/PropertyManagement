using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Repositories;

public interface ILeaseRepository
{
    Task<Lease?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingLeaseAsync(
        Guid unitId,
        Guid organizationId,
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludingLeaseId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Lease>> GetByUnitIdAsync(
        Guid unitId,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<Lease?> GetActiveLeaseForUnitAsync(
        Guid unitId,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Lease lease, CancellationToken cancellationToken = default);

    void Update(Lease lease);
}
