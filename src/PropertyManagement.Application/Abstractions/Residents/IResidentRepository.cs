using PropertyManagement.Domain.Entities.Residents;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Residents;

public interface IResidentRepository
{
    Task<Resident?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Resident?> GetByIdWithContactsAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Resident> Items, int TotalCount)> GetPagedAsync(
        Guid organizationId,
        string? searchTerm,
        ResidentStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        LeaseResidentRole? leaseRole = null,
        bool onlyLeaseHolders = false);
    Task<bool> IsLeaseHolderAsync(Guid residentId, Guid organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(Resident resident, CancellationToken cancellationToken = default);
    void SetRowVersion(Resident resident, byte[] rowVersion);
    void RemoveEmergencyContact(EmergencyContact contact);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
