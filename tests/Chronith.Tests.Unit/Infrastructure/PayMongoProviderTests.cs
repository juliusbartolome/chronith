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
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var signatureHeader = BuildSignatureHeader(rawBody, WebhookSecret, timestamp);

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhookSignature_TamperedBody_ReturnsFalse()
    {
        const string originalBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        const string tamperedBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-999"}}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var signatureHeader = BuildSignatureHeader(originalBody, WebhookSecret, timestamp);

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
    public void ValidateWebhookSignature_ExpiredTimestamp_ReturnsFalse()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();

        var signatureHeader = BuildSignatureHeader(rawBody, WebhookSecret, expiredTimestamp);

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_FutureTimestampBeyondTolerance_ReturnsFalse()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();

        var signatureHeader = BuildSignatureHeader(rawBody, WebhookSecret, futureTimestamp);

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_ValidTimestampWithinTolerance_ReturnsTrue()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid","reference_number":"ref-123"}}}""";
        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var signatureHeader = BuildSignatureHeader(rawBody, WebhookSecret, currentTimestamp);

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeTrue();
    }

    private static string BuildSignatureHeader(string rawBody, string secret, string timestamp)
    {
        var toSign = $"{timestamp}.{rawBody}";
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
        var hexSignature = Convert.ToHexStringLower(hash);
        return $"t={timestamp},te={hexSignature}";
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
