using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class AuditRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditRetentionOptions> options,
    ILogger<AuditRetentionService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredEntriesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during audit log retention cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(options.Value.IntervalHours), stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task PurgeExpiredEntriesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditEntryRepository>();

        var tenantIds = await auditRepo.GetDistinctTenantIdsAsync(ct);
        if (tenantIds.Count == 0)
        {
            logger.LogDebug("No audit entries found; skipping retention cleanup");
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.RetentionDays);
        var totalDeleted = 0;

        foreach (var tenantId in tenantIds)
        {
            try
            {
                var deleted = await auditRepo.DeleteExpiredAsync(tenantId, cutoff, ct);
                totalDeleted += deleted;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error purging audit entries for tenant {TenantId}", tenantId);
            }
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation(
                "Audit retention cleanup: deleted {Count} entries older than {Cutoff} across {TenantCount} tenants",
                totalDeleted, cutoff, tenantIds.Count);
        }
    }
}
