using System.Text.Json.Serialization;

namespace Chronith.Application.DTOs;

public sealed record CreateCheckoutRequest(
    long AmountInCentavos,
    string Currency,
    string Description,
    Guid BookingId,
    Guid TenantId);

public sealed record CreateCheckoutResult(
    string CheckoutUrl,
    string ProviderTransactionId);

public sealed record WebhookValidationContext(
    IDictionary<string, string> Headers,
    string RawBody,
    string? SourceIpAddress);

public sealed record WebhookPaymentEvent(
    string ProviderTransactionId,
    PaymentEventType EventType);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentEventType
{
    Success,
    Failed,
    Expired,
    Cancelled
}
