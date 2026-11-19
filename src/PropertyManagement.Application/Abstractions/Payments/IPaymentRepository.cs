using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Payments;

public sealed record PaymentFilter(
    Guid OrganizationId,
    Guid? ResidentId = null,
    Guid? LeaseId = null,
    PaymentStatus? Status = null,
    PaymentMethod? PaymentMethod = null,
    PaymentProviderType? Provider = null,
    DateTime? CreatedFromUtc = null,
    DateTime? CreatedToUtc = null,
    bool OnlyUnapplied = false,
    int PageNumber = 1,
    int PageSize = 10,
    bool IncludeDetails = false,
    bool AsNoTracking = false);

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdIgnoringFiltersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByProviderPaymentIdAsync(PaymentProviderType provider, string providerPaymentId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByProviderTransactionIdAsync(PaymentProviderType provider, string providerTransactionId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Payment> Items, int TotalCount)> GetPagedAsync(PaymentFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByProviderAndWindowAsync(
        Guid organizationId,
        PaymentProviderType provider,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
    Task AcquirePaymentLockAsync(Guid paymentId, Guid organizationId, CancellationToken cancellationToken = default);
    Task AcquireResidentLockAsync(Guid residentId, Guid organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    void SetRowVersion(Payment payment, byte[] rowVersion);
    void Detach(Payment payment);
}
