using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Chronith.API.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
