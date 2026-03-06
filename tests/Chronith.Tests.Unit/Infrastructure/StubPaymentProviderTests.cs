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
