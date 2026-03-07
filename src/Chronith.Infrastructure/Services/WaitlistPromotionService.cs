using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class WaitlistPromotionService(
    IServiceScopeFactory scopeFactory,
    IOptions<WaitlistPromotionOptions> options,
    ILogger<WaitlistPromotionService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOffersAndPromoteAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error in waitlist promotion");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.CheckIntervalSeconds), stoppingToken);
        }
    }

    // internal to allow unit tests via InternalsVisibleTo
    internal async Task ProcessExpiredOffersAndPromoteAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var waitlistRepo = scope.ServiceProvider.GetRequiredService<IWaitlistRepository>();

        var now = DateTimeOffset.UtcNow;
        var offerTtl = TimeSpan.FromMinutes(options.Value.OfferTtlMinutes);

        // 1. Find all Offered entries past their ExpiresAt → mark Expired
        var expiredOffers = await waitlistRepo.GetExpiredOffersAsync(now, ct);

        foreach (var entry in expiredOffers)
        {
            entry.Expire();
            await waitlistRepo.UpdateAsync(entry, ct);

            logger.LogInformation(
                "Expired waitlist offer {EntryId} for BookingType {BookingTypeId} slot {Start}-{End}",
                entry.Id, entry.BookingTypeId, entry.DesiredStart, entry.DesiredEnd);

            // 2. Promote the next Waiting entry for the same slot
            var next = await waitlistRepo.GetNextWaitingAsync(
                entry.TenantId, entry.BookingTypeId, entry.DesiredStart, entry.DesiredEnd, ct);

            if (next is not null)
            {
                next.Offer(now, offerTtl);
                await waitlistRepo.UpdateAsync(next, ct);

                logger.LogInformation(
                    "Offered waitlist entry {EntryId} to customer {CustomerId} for slot {Start}-{End}",
                    next.Id, next.CustomerId, next.DesiredStart, next.DesiredEnd);
            }
        }
    }
}
