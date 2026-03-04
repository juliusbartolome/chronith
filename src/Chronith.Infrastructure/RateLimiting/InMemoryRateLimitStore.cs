using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.RateLimiting;

public sealed class InMemoryRateLimitStore(IOptions<RateLimitingOptions> options) : IRateLimitStore
{
    private readonly RateLimitingOptions _options = options.Value;

    public int GetPermitLimit(string tenantId)
    {
        if (_options.TenantOverrides.TryGetValue(tenantId, out var @override))
            return @override.PermitLimit;

        return _options.DefaultPermitLimit;
    }
}
