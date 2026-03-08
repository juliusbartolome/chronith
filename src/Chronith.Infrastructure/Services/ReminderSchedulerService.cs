using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class ReminderSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderSchedulerOptions> options,
    ILogger<ReminderSchedulerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during reminder scheduling check");
            }
            await Task.Delay(
                TimeSpan.FromMinutes(options.Value.CheckIntervalMinutes), stoppingToken);
        }
    }

    internal async Task CheckRemindersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
        var reminderRepo = scope.ServiceProvider.GetRequiredService<IBookingReminderRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
        var configRepo = scope.ServiceProvider.GetRequiredService<INotificationConfigRepository>();

        // Get all booking types that have reminder intervals configured
        var bookingTypesWithReminders = await db.BookingTypes
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(bt => bt.ReminderIntervals != null && !bt.IsDeleted)
            .Select(bt => new
            {
                bt.Id,
                bt.TenantId,
                bt.Slug,
                bt.ReminderIntervals
            })
            .ToListAsync(ct);

        if (bookingTypesWithReminders.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        foreach (var bookingType in bookingTypesWithReminders)
        {
            var intervals = ParseIntervals(bookingType.ReminderIntervals);
            if (intervals.Count == 0) continue;

            // Check if this tenant has any enabled notification channels
            var enabledConfigs = await configRepo.ListEnabledByTenantAsync(
                bookingType.TenantId, ct);
            if (enabledConfigs.Count == 0) continue;

            // For each interval, find confirmed bookings starting within the window
            foreach (var intervalMinutes in intervals)
            {
                // Booking should start within [now + interval, now + interval + checkInterval]
                // This means: reminder is due when (booking.Start - interval) <= now
                // And booking hasn't started yet: booking.Start > now
                var targetTime = now.AddMinutes(intervalMinutes);
                var windowEnd = targetTime.AddMinutes(options.Value.CheckIntervalMinutes);

                var bookings = await db.Bookings
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(b => b.BookingTypeId == bookingType.Id
                             && b.Status == BookingStatus.Confirmed
                             && !b.IsDeleted
                             && b.Start >= targetTime
                             && b.Start < windowEnd)
                    .Select(b => new
                    {
                        b.Id,
                        b.TenantId,
                        b.CustomerEmail,
                        b.Start,
                        b.End
                    })
                    .ToListAsync(ct);

                foreach (var booking in bookings)
                {
                    // Check if reminder already sent
                    var alreadySent = await reminderRepo.ExistsAsync(
                        booking.Id, intervalMinutes, ct);
                    if (alreadySent) continue;

                    // Record the reminder
                    var reminder = new BookingReminder
                    {
                        BookingId = booking.Id,
                        IntervalMinutes = intervalMinutes,
                        SentAt = now
                    };
                    await reminderRepo.AddAsync(reminder, ct);

                    // Write notification outbox entries for each enabled channel
                    var payload = JsonSerializer.Serialize(new
                    {
                        @event = "notification.booking_reminder",
                        bookingId = booking.Id,
                        tenantId = booking.TenantId,
                        bookingTypeSlug = bookingType.Slug,
                        status = "Confirmed",
                        start = booking.Start,
                        end = booking.End,
                        customerId = string.Empty,
                        customerEmail = booking.CustomerEmail,
                        occurredAt = now,
                        reminderMinutesBefore = intervalMinutes
                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

                    var entries = enabledConfigs.Select(config => new WebhookOutboxEntry
                    {
                        TenantId = booking.TenantId,
                        WebhookId = null,
                        BookingTypeId = bookingType.Id,
                        BookingId = booking.Id,
                        EventType = $"notification.booking_reminder.{config.ChannelType}",
                        Payload = payload,
                        Category = OutboxCategory.Notification,
                    }).ToList();

                    await outboxRepo.AddRangeAsync(entries, ct);

                    logger.LogInformation(
                        "Scheduled {Count} reminder(s) for booking {BookingId} ({Interval}m before start)",
                        entries.Count, booking.Id, intervalMinutes);
                }
            }
        }

        // Save all added reminders and outbox entries
        await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<int> ParseIntervals(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
