using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class WebhookOutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<WebhookOutboxCleanupOptions> options,
    ILogger<WebhookOutboxCleanupService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(options.Value.IntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredEntriesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "WebhookOutboxCleanupService: error during cleanup iteration.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task PurgeExpiredEntriesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.RetentionDays);
        var deleted = await repo.DeleteOlderThanAsync(cutoff, ct);
        if (deleted > 0)
            logger.LogInformation(
                "WebhookOutboxCleanupService: purged {Count} outbox rows older than {Cutoff}.",
                deleted, cutoff);
    }
}
