namespace Chronith.Application.Interfaces;

public interface ITenantPaymentProviderResolver
{
    Task<IPaymentProvider?> ResolveAsync(Guid tenantId, string providerName, CancellationToken ct = default);
}
