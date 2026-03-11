using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class ApiKeyAgingService(
    IServiceScopeFactory scopeFactory,
    IOptions<ApiKeyAgingOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<ApiKeyAgingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAgingAsync(stoppingToken);
                healthTracker.RecordSuccess(nameof(ApiKeyAgingService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during API key aging check");
            }

            await Task.Delay(TimeSpan.FromHours(options.Value.CheckIntervalHours), stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task CheckAgingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tenantRepo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var apiKeyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var tenants = await tenantRepo.ListAllAsync(ct);
        if (tenants.Count == 0)
        {
            logger.LogDebug("No tenants found; skipping API key aging check");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var threshold = TimeSpan.FromDays(options.Value.ThresholdDays);

        foreach (var tenant in tenants)
        {
            try
            {
                var keys = await apiKeyRepo.ListByTenantAsync(tenant.Id, ct);
                foreach (var key in keys)
                {
                    if (key.IsRevoked || key.IsExpired(now))
                        continue;

                    var age = now - key.CreatedAt;
                    if (age >= threshold)
                    {
                        logger.LogWarning(
                            "API key {KeyId} (tenant {TenantId}, description: '{Description}') is {AgeDays} days old (threshold: {ThresholdDays} days). Consider rotating it.",
                            key.Id,
                            key.TenantId,
                            key.Description,
                            (int)age.TotalDays,
                            options.Value.ThresholdDays);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error checking API key aging for tenant {TenantId}", tenant.Id);
            }
        }
    }
}
