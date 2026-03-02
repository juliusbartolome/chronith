namespace Chronith.Application.Interfaces;

public interface IPaymentProviderFactory
{
    IPaymentProvider GetProvider(string providerName);
}
