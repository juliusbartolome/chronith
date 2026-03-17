using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;

namespace Chronith.Infrastructure.Payments;

public sealed class StubPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Stub";

    // ── New API ───────────────────────────────────────────────────────────────

    public Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request, CancellationToken ct)
    {
        var transactionId = $"stub_{Guid.NewGuid():N}";
        return Task.FromResult(new CreateCheckoutResult(
            CheckoutUrl: $"https://stub-checkout.local/{transactionId}",
            ProviderTransactionId: transactionId));
    }

    public bool ValidateWebhook(WebhookValidationContext context) => true;

    public WebhookPaymentEvent ParseWebhookPayload(string rawBody)
    {
        return new WebhookPaymentEvent(
            ProviderTransactionId: $"stub_{Guid.NewGuid():N}",
            EventType: PaymentEventType.Success);
    }

    // ── Legacy API (kept until CreateBookingCommand migration in Task 12) ────

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking, string currency, CancellationToken ct)
    {
        var externalId = $"stub-{booking.Id}";
        var checkoutUrl = $"https://stub.example.com/checkout/{externalId}";
        return Task.FromResult(new PaymentIntentResult(externalId, checkoutUrl));
    }

    public bool ValidateWebhookSignature(string rawBody, string signatureHeader) => true;

    public PaymentEvent ParsePaymentEvent(string rawBody)
    {
        return new PaymentEvent(ExternalId: "stub", IsPaid: true);
    }
}
