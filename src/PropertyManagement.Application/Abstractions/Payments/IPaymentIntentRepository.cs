using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Application.Abstractions.Payments;

public interface IPaymentIntentRepository
{
    Task<PaymentIntent?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default);
    Task<PaymentIntent?> GetByProviderIntentIdAsync(string providerIntentId, CancellationToken cancellationToken = default);
    Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default);
    void SetRowVersion(PaymentIntent intent, byte[] rowVersion);
    void Detach(PaymentIntent intent);
}
