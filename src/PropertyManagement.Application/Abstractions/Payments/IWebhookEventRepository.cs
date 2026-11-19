using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Abstractions.Payments;

public interface IWebhookEventRepository
{
    Task<ProcessedWebhookEvent?> GetByProviderEventIdAsync(
        PaymentProviderType provider,
        string providerEventId,
        CancellationToken cancellationToken = default);
    Task AddAsync(ProcessedWebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}
