using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronith.Infrastructure.Payments.PayMongo;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class PayMongoProviderTests
{
    private readonly PayMongoProvider _provider;
    private const string WebhookSecret = "test-webhook-secret";

    public PayMongoProviderTests()
    {
        var options = Options.Create(new PayMongoOptions { WebhookSecret = WebhookSecret });
        _provider = new PayMongoProvider(options, Substitute.For<IHttpClientFactory>()); // IHttpClientFactory not needed for signature tests
    }

    [Fact]
    public void ValidateWebhookSignature_ValidSignature_ReturnsTrue()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        const string timestamp = "1700000000";

        var toSign = $"{timestamp}.{rawBody}";
        var key = Encoding.UTF8.GetBytes(WebhookSecret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
        var hexSignature = Convert.ToHexStringLower(hash);

        var signatureHeader = $"t={timestamp},te={hexSignature}";

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhookSignature_TamperedBody_ReturnsFalse()
    {
        const string originalBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        const string tamperedBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-999"}}}""";
        const string timestamp = "1700000000";

        var toSign = $"{timestamp}.{originalBody}";
        var key = Encoding.UTF8.GetBytes(WebhookSecret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
        var hexSignature = Convert.ToHexStringLower(hash);

        var signatureHeader = $"t={timestamp},te={hexSignature}";

        _provider.ValidateWebhookSignature(tamperedBody, signatureHeader).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_InvalidHeader_ReturnsFalse()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid"}}}""";
        const string malformedHeader = "not-a-valid-header";

        _provider.ValidateWebhookSignature(rawBody, malformedHeader).Should().BeFalse();
    }

    [Fact]
    public void ParsePaymentEvent_PaidStatus_ReturnsPaidTrue()
    {
        const string referenceNumber = "ref-abc-123";
        var rawBody = JsonSerializer.Serialize(new
        {
            data = new
            {
                attributes = new
                {
                    status = "paid",
                    reference_number = referenceNumber
                }
            }
        });

        var result = _provider.ParsePaymentEvent(rawBody);

        result.IsPaid.Should().BeTrue();
        result.ExternalId.Should().Be(referenceNumber);
    }

    [Fact]
    public void ParsePaymentEvent_NonPaidStatus_ReturnsPaidFalse()
    {
        var rawBody = JsonSerializer.Serialize(new
        {
            data = new
            {
                attributes = new
                {
                    status = "pending",
                    reference_number = "ref-xyz"
                }
            }
        });

        var result = _provider.ParsePaymentEvent(rawBody);

        result.IsPaid.Should().BeFalse();
    }
}
