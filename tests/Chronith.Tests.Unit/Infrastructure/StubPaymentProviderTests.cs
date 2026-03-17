using Chronith.Application.DTOs;
using Chronith.Infrastructure.Payments;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class StubPaymentProviderTests
{
    private readonly StubPaymentProvider _provider = new();

    [Fact]
    public void ProviderName_Returns_Stub()
    {
        _provider.ProviderName.Should().Be("Stub");
    }

    // ── New API ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsStubUrlAndTransactionId()
    {
        var request = new CreateCheckoutRequest(
            AmountInCentavos: 50000,
            Currency: "PHP",
            Description: "Test",
            BookingId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        var result = await _provider.CreateCheckoutSessionAsync(request, CancellationToken.None);

        result.CheckoutUrl.Should().StartWith("https://stub-checkout.local/");
        result.ProviderTransactionId.Should().StartWith("stub_");
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_ReturnsDifferentTransactionIds()
    {
        var request = new CreateCheckoutRequest(
            AmountInCentavos: 50000,
            Currency: "PHP",
            Description: "Test",
            BookingId: Guid.NewGuid(),
            TenantId: Guid.NewGuid());

        var result1 = await _provider.CreateCheckoutSessionAsync(request, CancellationToken.None);
        var result2 = await _provider.CreateCheckoutSessionAsync(request, CancellationToken.None);

        result1.ProviderTransactionId.Should().NotBe(result2.ProviderTransactionId);
    }

    [Fact]
    public void ValidateWebhook_AlwaysReturnsTrue()
    {
        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: "127.0.0.1");

        _provider.ValidateWebhook(context).Should().BeTrue();
    }

    [Fact]
    public void ParseWebhookPayload_ReturnsSuccessEvent()
    {
        var result = _provider.ParseWebhookPayload("{\"transactionId\": \"stub_123\"}");

        result.EventType.Should().Be(PaymentEventType.Success);
        result.ProviderTransactionId.Should().NotBeNullOrEmpty();
    }

    // ── Legacy API (kept until CreateBookingCommand migration in Task 12) ────

    [Fact]
    public async Task CreatePaymentIntentAsync_Returns_DeterministicResult()
    {
        var booking = new BookingBuilder().Build();

        var result = await _provider.CreatePaymentIntentAsync(booking, "PHP", CancellationToken.None);

        result.ExternalId.Should().NotBeEmpty();
        result.CheckoutUrl.Should().StartWith("https://stub.example.com/checkout/");
    }

    [Fact]
    public void ValidateWebhookSignature_AlwaysReturnsTrue()
    {
        _provider.ValidateWebhookSignature("{}", "any-signature").Should().BeTrue();
    }

    [Fact]
    public void ParsePaymentEvent_ReturnsPaidEvent()
    {
        var result = _provider.ParsePaymentEvent("{\"externalId\":\"stub-1\"}");
        result.IsPaid.Should().BeTrue();
    }
}
