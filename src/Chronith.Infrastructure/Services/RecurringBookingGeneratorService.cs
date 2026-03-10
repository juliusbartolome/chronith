using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class RecurringBookingGeneratorService(
    IServiceScopeFactory scopeFactory,
    IOptions<RecurringBookingGeneratorOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ILogger<RecurringBookingGeneratorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateBookingsAsync(stoppingToken);
                healthTracker.RecordSuccess(nameof(RecurringBookingGeneratorService));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during recurring booking generation");
            }

            await Task.Delay(TimeSpan.FromHours(options.Value.CheckIntervalHours), stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task GenerateBookingsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var recurrenceRuleRepo = scope.ServiceProvider.GetRequiredService<IRecurrenceRuleRepository>();

        var rules = await recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(ct);
        if (rules.Count == 0)
        {
            logger.LogDebug("No active recurrence rules found");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(options.Value.GenerationHorizonDays);

        logger.LogInformation(
            "Processing {RuleCount} active recurrence rules for horizon {From} to {To}",
            rules.Count, today, horizon);

        // TODO: Create bookings from computed occurrences (deferred to future task).
        // Currently this service only computes and logs occurrences for observability.
        foreach (var rule in rules)
        {
            var occurrences = rule.ComputeOccurrences(today, horizon);

            foreach (var occurrence in occurrences)
            {
                logger.LogDebug(
                    "Recurrence rule {RuleId} (Tenant {TenantId}, BookingType {BookingTypeId}): " +
                    "occurrence on {OccurrenceDate}",
                    rule.Id, rule.TenantId, rule.BookingTypeId, occurrence);
            }

            if (occurrences.Count > 0)
            {
                logger.LogInformation(
                    "Recurrence rule {RuleId}: {OccurrenceCount} occurrences computed in horizon",
                    rule.Id, occurrences.Count);
            }
        }
    }
}
