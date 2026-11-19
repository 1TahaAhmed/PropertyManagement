using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Payments;

public sealed class WebhookEventRepository(ApplicationDbContext context) : IWebhookEventRepository
{
    public async Task<ProcessedWebhookEvent?> GetByProviderEventIdAsync(
        PaymentProviderType provider,
        string providerEventId,
        CancellationToken cancellationToken = default)
    {
        var normalized = providerEventId.Trim();
        return await context.ProcessedWebhookEvents
            .FirstOrDefaultAsync(w => w.Provider == provider && w.ProviderEventId == normalized, cancellationToken);
    }

    public async Task AddAsync(ProcessedWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        await context.ProcessedWebhookEvents.AddAsync(webhookEvent, cancellationToken);
    }
}
