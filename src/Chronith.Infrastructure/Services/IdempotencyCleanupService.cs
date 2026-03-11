using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class IdempotencyCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdempotencyOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<IdempotencyCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IIdempotencyKeyRepository>();
                await repo.DeleteExpiredAsync(stoppingToken);
                logger.LogInformation("Expired idempotency keys cleaned up");
                healthTracker.RecordSuccess(nameof(IdempotencyCleanupService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during idempotency key cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(options.Value.CleanupIntervalHours), stoppingToken);
        }
    }
}
