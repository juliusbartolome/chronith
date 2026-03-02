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

    public bool ValidateWebhookSignature(string rawBody, string signatureHeader) => true;

    public PaymentEvent ParsePaymentEvent(string rawBody)
    {
        // Minimal: assume any stub webhook is a successful payment
        return new PaymentEvent(ExternalId: "stub", IsPaid: true);
    }
}
