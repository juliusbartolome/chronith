using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.RateLimiting;

/// <summary>
/// Redis-backed IRateLimitStore. Returns the configured per-tenant permit limit,
/// delegating counter storage to ASP.NET Core's built-in RateLimiter middleware.
/// Registered in DI only when Redis:Enabled = true; InMemoryRateLimitStore
/// is used as the fallback otherwise.
/// </summary>
public sealed class RedisRateLimitStore(IOptions<RateLimitingOptions> options) : IRateLimitStore
{
    private readonly RateLimitingOptions _options = options.Value;

    public int GetPermitLimit(string tenantId)
    {
        if (_options.TenantOverrides.TryGetValue(tenantId, out var @override)
            && @override.PermitLimit.HasValue)
            return @override.PermitLimit.Value;

        return _options.Authenticated.PermitLimit;
    }
}
