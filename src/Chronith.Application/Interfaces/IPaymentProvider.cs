using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request,
        CancellationToken ct);

    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking,
        string currency,
        CancellationToken ct);

    bool ValidateWebhook(WebhookValidationContext context);

    bool ValidateWebhookSignature(string rawBody, string signatureHeader);

    WebhookPaymentEvent ParseWebhookPayload(string rawBody);

    PaymentEvent ParsePaymentEvent(string rawBody);
}

public sealed record PaymentIntentResult(string ExternalId, string CheckoutUrl);
public sealed record PaymentEvent(string ExternalId, bool IsPaid);
