using PropertyManagement.Application.Abstractions.Payments;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Payments;

public sealed class PaymentProviderFactory(IEnumerable<IPaymentProvider> providers) : IPaymentProviderFactory
{
    private readonly Dictionary<PaymentProviderType, IPaymentProvider> _providers =
        providers.ToDictionary(p => p.ProviderType);

    public IPaymentProvider Resolve(PaymentProviderType providerType)
    {
        if (_providers.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        if (providerType == PaymentProviderType.Bank &&
            _providers.TryGetValue(PaymentProviderType.Manual, out var manualFallback))
        {
            return manualFallback;
        }

        throw new NotSupportedException($"Payment provider '{providerType}' is not registered or supported.");
    }
}
