using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using AppTelemetry = Chronith.Application.Telemetry;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Telemetry;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class RecurringBookingGeneratorService(
    IServiceScopeFactory scopeFactory,
    IOptions<RecurringBookingGeneratorOptions> options,
    IBackgroundServiceHealthTracker healthTracker,
    ChronithMetrics metrics,
    ILogger<RecurringBookingGeneratorService> logger)
    : BackgroundService
{
    private static readonly BookingStatus[] ConflictStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingVerification,
        BookingStatus.Confirmed
    ];

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
        var bookingTypeRepo = scope.ServiceProvider.GetRequiredService<IBookingTypeRepository>();
        var tenantRepo = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
        var customerRepo = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

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

        foreach (var rule in rules)
        {
            try
            {
                await ProcessRuleAsync(
                    rule, today, horizon,
                    recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo,
                    bookingRepo, unitOfWork, publisher, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Error processing recurrence rule {RuleId} (Tenant {TenantId}, BookingType {BookingTypeId})",
                    rule.Id, rule.TenantId, rule.BookingTypeId);
            }
        }
    }

    private async Task ProcessRuleAsync(
        RecurrenceRule rule,
        DateOnly today,
        DateOnly horizon,
        IRecurrenceRuleRepository recurrenceRuleRepo,
        IBookingTypeRepository bookingTypeRepo,
        ITenantRepository tenantRepo,
        ICustomerRepository customerRepo,
        IBookingRepository bookingRepo,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        CancellationToken ct)
    {
        // Deactivate rules whose series has ended
        if (rule.SeriesEnd.HasValue && rule.SeriesEnd.Value <= today)
        {
            rule.SoftDelete();
            recurrenceRuleRepo.Update(rule);
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation(
                "Deactivated recurrence rule {RuleId}: series ended on {SeriesEnd}",
                rule.Id, rule.SeriesEnd);
            return;
        }

        // Resolve booking type (cross-tenant)
        var bookingType = await bookingTypeRepo.GetByIdAcrossTenantsAsync(rule.BookingTypeId, ct);
        if (bookingType is null)
        {
            logger.LogWarning(
                "Recurrence rule {RuleId}: booking type {BookingTypeId} not found — skipping",
                rule.Id, rule.BookingTypeId);
            return;
        }

        using var activity = AppTelemetry.ChronithActivitySource.StartRecurringGenerate(rule.TenantId, rule.Id);

        // Resolve tenant (for timezone)
        var tenant = await tenantRepo.GetByIdAsync(rule.TenantId, ct);
        if (tenant is null)
        {
            logger.LogWarning(
                "Recurrence rule {RuleId}: tenant {TenantId} not found — skipping",
                rule.Id, rule.TenantId);
            return;
        }

        // Resolve customer (for email)
        var customer = await customerRepo.GetByIdAcrossTenantsAsync(rule.CustomerId, ct);
        if (customer is null)
        {
            logger.LogWarning(
                "Recurrence rule {RuleId}: customer {CustomerId} not found — skipping",
                rule.Id, rule.CustomerId);
            return;
        }

        var tz = tenant.GetTimeZone();
        var occurrences = rule.ComputeOccurrences(today, horizon);

        if (occurrences.Count == 0)
        {
            logger.LogDebug(
                "Recurrence rule {RuleId}: no occurrences in horizon {From} to {To}",
                rule.Id, today, horizon);
            return;
        }

        logger.LogInformation(
            "Recurrence rule {RuleId}: {OccurrenceCount} occurrences computed in horizon",
            rule.Id, occurrences.Count);

        foreach (var occurrence in occurrences)
        {
            await ProcessOccurrenceAsync(
                rule, occurrence, tz, bookingType, customer,
                bookingRepo, unitOfWork, publisher, ct);
        }
    }

    private async Task ProcessOccurrenceAsync(
        RecurrenceRule rule,
        DateOnly occurrence,
        TenantTimeZone tz,
        BookingType bookingType,
        Customer customer,
        IBookingRepository bookingRepo,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        CancellationToken ct)
    {
        // Convert local date + time to UTC
        var start = tz.ToUtc(occurrence, rule.StartTime);
        var end = start + rule.Duration;

        // Get effective conflict range (respects buffers)
        var (effStart, effEnd) = bookingType.GetEffectiveRange(start, end);

        // Check if slot is full
        var conflictCount = await bookingRepo.CountConflictsAsync(
            bookingType.Id, effStart, effEnd, ConflictStatuses, ct);

        if (conflictCount >= bookingType.Capacity)
        {
            logger.LogDebug(
                "Recurrence rule {RuleId}: occurrence {Occurrence} skipped — slot full ({Conflicts}/{Capacity})",
                rule.Id, occurrence, conflictCount, bookingType.Capacity);
            return;
        }

        var booking = Booking.Create(
            tenantId: rule.TenantId,
            bookingTypeId: bookingType.Id,
            start: start,
            end: end,
            customerId: rule.CustomerId.ToString(),
            customerEmail: customer.Email,
            amountInCentavos: bookingType.PriceInCentavos,
            currency: bookingType.Currency);

        await bookingRepo.AddAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        metrics.RecordBookingCreated(
            rule.TenantId.ToString(),
            bookingType is TimeSlotBookingType ? "TimeSlot" : "Calendar");

        logger.LogInformation(
            "Recurrence rule {RuleId}: created booking {BookingId} for occurrence {Occurrence}",
            rule.Id, booking.Id, occurrence);

        await publisher.Publish(
            new BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType.Slug,
                FromStatus: null,
                ToStatus: booking.Status,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail),
            ct);
    }
}
