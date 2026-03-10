using System.Diagnostics;

namespace Chronith.Infrastructure.Telemetry;

public static class ChronithActivitySource
{
    public const string Name = "Chronith.API";
    public static readonly ActivitySource Instance = new(Name, "0.8.0");

    public static Activity? StartBookingStateTransition(string operation, Guid tenantId, Guid bookingId)
        => Instance.StartActivity("chronith.booking.state_transition")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("booking.operation", operation)
            ?.AddTag("booking.id", bookingId.ToString());

    public static Activity? StartPaymentProcess(Guid tenantId, string provider)
        => Instance.StartActivity("chronith.payment.process")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("payment.provider", provider);

    public static Activity? StartWebhookDispatch(Guid tenantId, Guid webhookId)
        => Instance.StartActivity("chronith.webhook.dispatch")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("webhook.id", webhookId.ToString());

    public static Activity? StartNotificationDispatch(Guid tenantId, string channel)
        => Instance.StartActivity("chronith.notification.dispatch")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("notification.channel", channel);

    public static Activity? StartAvailabilityCompute(Guid tenantId, string bookingTypeSlug)
        => Instance.StartActivity("chronith.availability.compute")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("booking_type.slug", bookingTypeSlug);

    public static Activity? StartRecurringGenerate(Guid tenantId, Guid recurrenceRuleId)
        => Instance.StartActivity("chronith.recurring.generate")
            ?.AddTag("tenant.id", tenantId.ToString())
            ?.AddTag("recurrence_rule.id", recurrenceRuleId.ToString());
}
