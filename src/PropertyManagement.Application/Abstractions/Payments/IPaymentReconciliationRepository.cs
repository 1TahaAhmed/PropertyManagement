using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Payments;

public sealed record ReconciliationFilter(
    Guid OrganizationId,
    Guid? BatchId = null,
    PaymentProviderType? Provider = null,
    ReconciliationStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20);

public interface IPaymentReconciliationRepository
{
    Task<PaymentReconciliationRecord?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PaymentReconciliationRecord> Items, int TotalCount)> GetPagedAsync(
        ReconciliationFilter filter,
        CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<PaymentReconciliationRecord> records, CancellationToken cancellationToken = default);
}
