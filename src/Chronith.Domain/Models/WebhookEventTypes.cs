namespace Chronith.Domain.Models;

public static class WebhookEventTypes
{
    public const string PaymentReceived = "booking.payment_received";
    public const string Confirmed = "booking.confirmed";
    public const string Cancelled = "booking.cancelled";
    public const string PaymentFailed = "booking.payment_failed";

    public static readonly IReadOnlyList<string> All =
    [
        PaymentReceived,
        Confirmed,
        Cancelled,
        PaymentFailed
    ];

    public static bool IsValid(string eventType) => All.Contains(eventType);
}
