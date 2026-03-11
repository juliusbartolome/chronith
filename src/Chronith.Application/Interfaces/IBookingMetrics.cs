namespace Chronith.Application.Interfaces;

public interface IBookingMetrics
{
    void RecordBookingCreated(string tenantId, string bookingKind);
    void RecordBookingConfirmed(string tenantId);
    void RecordBookingCancelled(string tenantId);
    void RecordPaymentProcessed(string tenantId, string provider);
    void RecordAvailabilityDuration(string tenantId, double durationMs);
    void RecordNotificationSent(string tenantId, string channel);
    void RecordWebhookDispatched(string tenantId);
}
