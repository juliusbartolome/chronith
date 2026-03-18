using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Payments.PayMongo;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Payments;

public sealed class TenantPaymentProviderResolver(
    ITenantPaymentConfigRepository configRepo,
    IHttpClientFactory httpClientFactory)
    : ITenantPaymentProviderResolver
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IPaymentProvider?> ResolveAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        if (providerName.Equals("Stub", StringComparison.OrdinalIgnoreCase))
            return new StubPaymentProvider();

        if (providerName.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return null;

        var config = await configRepo.GetActiveByProviderNameAsync(tenantId, providerName, ct);
        if (config is null) return null;

        return providerName.ToUpperInvariant() switch
        {
            "PAYMONGO" => BuildPayMongo(config.Settings),
            "MAYA"     => BuildMaya(config.Settings),
            _          => null
        };
    }

    private IPaymentProvider BuildPayMongo(string settings)
    {
        var opts = JsonSerializer.Deserialize<PayMongoOptions>(settings, JsonOpts) ?? new PayMongoOptions();
        return new PayMongoProvider(Options.Create(opts), httpClientFactory);
    }

    private IPaymentProvider BuildMaya(string settings)
    {
        var opts = JsonSerializer.Deserialize<MayaOptions>(settings, JsonOpts) ?? new MayaOptions();
        return new MayaProvider(Options.Create(opts), httpClientFactory);
    }
}
