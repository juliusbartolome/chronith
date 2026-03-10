using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Chronith.Infrastructure.Telemetry;

public sealed class ChronithMetrics
{
    public const string MeterName = "Chronith.API";

    private readonly Counter<long> _bookingsCreated;
    private readonly Counter<long> _bookingsConfirmed;
    private readonly Counter<long> _bookingsCancelled;
    private readonly Counter<long> _paymentsProcessed;
    private readonly Counter<long> _webhooksDispatched;
    private readonly Counter<long> _notificationsSent;
    private readonly Histogram<double> _availabilityDuration;

    public ChronithMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _bookingsCreated = meter.CreateCounter<long>("chronith.bookings.created");
        _bookingsConfirmed = meter.CreateCounter<long>("chronith.bookings.confirmed");
        _bookingsCancelled = meter.CreateCounter<long>("chronith.bookings.cancelled");
        _paymentsProcessed = meter.CreateCounter<long>("chronith.payments.processed");
        _webhooksDispatched = meter.CreateCounter<long>("chronith.webhooks.dispatched");
        _notificationsSent = meter.CreateCounter<long>("chronith.notifications.sent");
        _availabilityDuration = meter.CreateHistogram<double>("chronith.availability.duration_ms");
    }

    public void RecordBookingCreated(string tenantId, string bookingKind) =>
        _bookingsCreated.Add(1, new TagList { { "tenant.id", tenantId }, { "booking.kind", bookingKind } });

    public void RecordBookingConfirmed(string tenantId) =>
        _bookingsConfirmed.Add(1, new TagList { { "tenant.id", tenantId } });

    public void RecordBookingCancelled(string tenantId) =>
        _bookingsCancelled.Add(1, new TagList { { "tenant.id", tenantId } });

    public void RecordPaymentProcessed(string tenantId, string provider) =>
        _paymentsProcessed.Add(1, new TagList { { "tenant.id", tenantId }, { "payment.provider", provider } });

    public void RecordWebhookDispatched(string tenantId) =>
        _webhooksDispatched.Add(1, new TagList { { "tenant.id", tenantId } });

    public void RecordNotificationSent(string tenantId, string channel) =>
        _notificationsSent.Add(1, new TagList { { "tenant.id", tenantId }, { "notification.channel", channel } });

    public void RecordAvailabilityDuration(string tenantId, double durationMs) =>
        _availabilityDuration.Record(durationMs, new TagList { { "tenant.id", tenantId } });
}
