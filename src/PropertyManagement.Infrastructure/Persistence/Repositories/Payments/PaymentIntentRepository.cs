using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Entities.Payments;

namespace PropertyManagement.Infrastructure.Persistence.Repositories.Payments;

public sealed class PaymentIntentRepository(ApplicationDbContext context) : IPaymentIntentRepository
{
    public async Task<PaymentIntent?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.Id == id && pi.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var normalized = idempotencyKey.Trim();
        return await context.PaymentIntents
            .FirstOrDefaultAsync(pi => pi.OrganizationId == organizationId && pi.IdempotencyKey == normalized, cancellationToken);
    }

    public async Task<PaymentIntent?> GetByProviderIntentIdAsync(string providerIntentId, CancellationToken cancellationToken = default)
    {
        var normalized = providerIntentId.Trim();
        return await context.PaymentIntents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(pi => pi.ProviderIntentId == normalized, cancellationToken);
    }

    public async Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        await context.PaymentIntents.AddAsync(intent, cancellationToken);
    }

    public void SetRowVersion(PaymentIntent intent, byte[] rowVersion)
    {
        context.Entry(intent).Property(pi => pi.RowVersion).OriginalValue = rowVersion;
    }

    public void Detach(PaymentIntent intent)
    {
        context.Entry(intent).State = EntityState.Detached;
    }
}
