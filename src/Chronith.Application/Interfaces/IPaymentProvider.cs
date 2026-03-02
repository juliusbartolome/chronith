using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking,
        string currency,
        CancellationToken ct);

    bool ValidateWebhookSignature(string rawBody, string signatureHeader);

    PaymentEvent ParsePaymentEvent(string rawBody);
}

public sealed record PaymentIntentResult(string ExternalId, string CheckoutUrl);
public sealed record PaymentEvent(string ExternalId, bool IsPaid);
