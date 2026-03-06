using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;

namespace Chronith.Infrastructure.Payments;

public sealed class StubPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Stub";

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(
        Booking booking, string currency, CancellationToken ct)
    {
        var externalId = $"stub-{booking.Id}";
        var checkoutUrl = $"https://stub.example.com/checkout/{externalId}";
        return Task.FromResult(new PaymentIntentResult(externalId, checkoutUrl));
    }

    public Task<CreateCheckoutResult> CreateCheckoutSessionAsync(
        CreateCheckoutRequest request, CancellationToken ct)
    {
        var txnId = $"stub-{request.BookingId}";
        var checkoutUrl = $"https://stub.example.com/checkout/{txnId}";
        return Task.FromResult(new CreateCheckoutResult(checkoutUrl, txnId));
    }

    public bool ValidateWebhookSignature(string rawBody, string signatureHeader) => true;

    public bool ValidateWebhook(WebhookValidationContext context) => true;

    public PaymentEvent ParsePaymentEvent(string rawBody)
    {
        // Minimal: assume any stub webhook is a successful payment
        return new PaymentEvent(ExternalId: "stub", IsPaid: true);
    }

    public WebhookPaymentEvent ParseWebhookPayload(string rawBody)
    {
        return new WebhookPaymentEvent(ProviderTransactionId: "stub", EventType: PaymentEventType.Success);
    }
}
