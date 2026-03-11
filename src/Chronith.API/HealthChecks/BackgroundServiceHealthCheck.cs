using Chronith.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Chronith.API.HealthChecks;

public sealed class BackgroundServiceHealthCheck(IBackgroundServiceHealthTracker healthTracker) : IHealthCheck
{
    private static readonly string[] ServiceNames =
    [
        "NotificationDispatcherService",
        "WebhookDispatcherService",
        "ReminderSchedulerService",
        "WaitlistPromotionService",
        "RecurringBookingGeneratorService",
        "IdempotencyCleanupService",
        "AuditRetentionService",
    ];

    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(30);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var stale = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var name in ServiceNames)
        {
            var lastSuccess = healthTracker.GetLastSuccess(name);
            if (lastSuccess is null)
            {
                // Service has not run yet — app just started, treat as healthy.
                continue;
            }

            if (now - lastSuccess.Value > StalenessThreshold)
            {
                stale.Add(name);
            }
        }

        if (stale.Count > 0)
        {
            var message = $"Stale background services: {string.Join(", ", stale)}";
            return Task.FromResult(HealthCheckResult.Degraded(message));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
