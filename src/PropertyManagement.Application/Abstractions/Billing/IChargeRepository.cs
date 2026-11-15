using PropertyManagement.Domain.Entities.Billing;

namespace PropertyManagement.Application.Abstractions.Billing;

public interface IChargeRepository
{
    Task<Charge?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Charge>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid organizationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Charge>> GetPendingByLeaseAsync(Guid leaseId, Guid organizationId, DateOnly? upToPeriodEnd = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Charge>> GetByInvoiceIdAsync(Guid invoiceId, Guid organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(Charge charge, CancellationToken cancellationToken = default);
    void Detach(Charge charge);
    void SetRowVersion(Charge charge, byte[] rowVersion);
    void Update(Charge charge);
}
