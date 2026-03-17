using Chronith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronith.API.HealthChecks;

public sealed class DatabaseHealthCheck(ChronithDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) // codeql[cs/catch-of-all-exceptions] intentional: health check must handle any DB failure
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
