using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronith.Application.DTOs;
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
        var options = Options.Create(new PayMongoOptions
        {
            WebhookSecret = WebhookSecret,
            SuccessUrl = "https://example.com/{bookingId}/success",
            FailureUrl = "https://example.com/{bookingId}/failed"
        });
        _provider = new PayMongoProvider(options, Substitute.For<IHttpClientFactory>());
    }

    [Fact]
    public void ProviderName_IsPayMongo()
    {
        _provider.ProviderName.Should().Be("PayMongo");
    }

    // ── Webhook Validation (HMAC) ────────────────────────────────────────────

    [Fact]
    public void ValidateWebhook_WithValidHmac_ReturnsTrue()
    {
        var body = """{"data":{"id":"cs_123"}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var hmac = ComputeHmac($"{timestamp}.{body}", WebhookSecret);
        var signature = $"t={timestamp},li={hmac},te={hmac}";

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string> { ["paymongo-signature"] = signature },
            RawBody: body,
            SourceIpAddress: null);

        _provider.ValidateWebhook(context).Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhook_WithInvalidHmac_ReturnsFalse()
    {
        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>
                { ["paymongo-signature"] = "t=123,li=invalid,te=invalid" },
            RawBody: "{}",
            SourceIpAddress: null);

        _provider.ValidateWebhook(context).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhook_WithMissingHeader_ReturnsFalse()
    {
        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: null);

        _provider.ValidateWebhook(context).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhook_WithTamperedBody_ReturnsFalse()
    {
        const string originalBody = """{"data":{"id":"cs_123"}}""";
        const string tamperedBody = """{"data":{"id":"cs_999"}}""";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var hmac = ComputeHmac($"{timestamp}.{originalBody}", WebhookSecret);
        var signature = $"t={timestamp},te={hmac}";

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string> { ["paymongo-signature"] = signature },
            RawBody: tamperedBody,
            SourceIpAddress: null);

        _provider.ValidateWebhook(context).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhook_ExpiredTimestamp_ReturnsFalse()
    {
        var body = """{"data":{"id":"cs_123"}}""";
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var hmac = ComputeHmac($"{expiredTimestamp}.{body}", WebhookSecret);
        var signature = $"t={expiredTimestamp},te={hmac}";

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string> { ["paymongo-signature"] = signature },
            RawBody: body,
            SourceIpAddress: null);

        _provider.ValidateWebhook(context).Should().BeFalse();
    }

    // ── Legacy Webhook Signature Validation ──────────────────────────────────

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
        _provider.ValidateWebhookSignature("{}", "not-a-valid-header").Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhookSignature_ExpiredTimestamp_ReturnsFalse()
    {
        const string rawBody = """{"data":{"attributes":{"status":"paid"}}}""";
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var signatureHeader = BuildSignatureHeader(rawBody, WebhookSecret, expiredTimestamp);

        _provider.ValidateWebhookSignature(rawBody, signatureHeader).Should().BeFalse();
    }

    // ── ParseWebhookPayload (checkout session events) ────────────────────────

    [Fact]
    public void ParseWebhookPayload_CheckoutSessionPaid_ReturnsSuccess()
    {
        var body = """
        {
            "data": {
                "attributes": {
                    "type": "checkout_session.payment.paid",
                    "data": {
                        "id": "cs_abc123",
                        "attributes": {
                            "payment_intent": {
                                "id": "pi_xyz",
                                "attributes": { "status": "succeeded" }
                            }
                        }
                    }
                }
            }
        }
        """;

        var result = _provider.ParseWebhookPayload(body);

        result.ProviderTransactionId.Should().Be("cs_abc123");
        result.EventType.Should().Be(PaymentEventType.Success);
    }

    [Fact]
    public void ParseWebhookPayload_PaymentPaid_ReturnsSuccess()
    {
        var body = """
        {
            "data": {
                "attributes": {
                    "type": "payment.paid",
                    "data": {
                        "id": "pay_xyz789",
                        "attributes": { "status": "paid" }
                    }
                }
            }
        }
        """;

        var result = _provider.ParseWebhookPayload(body);

        result.ProviderTransactionId.Should().Be("pay_xyz789");
        result.EventType.Should().Be(PaymentEventType.Success);
    }

    [Fact]
    public void ParseWebhookPayload_PaymentFailed_ReturnsFailed()
    {
        var body = """
        {
            "data": {
                "attributes": {
                    "type": "payment.failed",
                    "data": {
                        "id": "pay_fail456",
                        "attributes": { "status": "failed" }
                    }
                }
            }
        }
        """;

        var result = _provider.ParseWebhookPayload(body);

        result.ProviderTransactionId.Should().Be("pay_fail456");
        result.EventType.Should().Be(PaymentEventType.Failed);
    }

    // ── Legacy ParsePaymentEvent ─────────────────────────────────────────────

    [Fact]
    public void ParsePaymentEvent_PaidStatus_ReturnsPaidTrue()
    {
        var rawBody = JsonSerializer.Serialize(new
        {
            data = new { attributes = new { status = "paid", reference_number = "ref-abc-123" } }
        });

        var result = _provider.ParsePaymentEvent(rawBody);

        result.IsPaid.Should().BeTrue();
        result.ExternalId.Should().Be("ref-abc-123");
    }

    [Fact]
    public void ParsePaymentEvent_NonPaidStatus_ReturnsPaidFalse()
    {
        var rawBody = JsonSerializer.Serialize(new
        {
            data = new { attributes = new { status = "pending", reference_number = "ref-xyz" } }
        });

        var result = _provider.ParsePaymentEvent(rawBody);

        result.IsPaid.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeHmac(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    private static string BuildSignatureHeader(string rawBody, string secret, string timestamp)
    {
        var toSign = $"{timestamp}.{rawBody}";
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(toSign));
        var hexSignature = Convert.ToHexStringLower(hash);
        return $"t={timestamp},te={hexSignature}";
    }
}
