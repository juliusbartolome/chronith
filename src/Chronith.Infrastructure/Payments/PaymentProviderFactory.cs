using Chronith.Application.Interfaces;

namespace Chronith.Infrastructure.Payments;

public sealed class PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    : IPaymentProviderFactory
{
    private readonly Dictionary<string, IPaymentProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public IPaymentProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"Payment provider '{providerName}' is not registered. " +
            $"Available: {string.Join(", ", _providers.Keys)}");
    }
}
