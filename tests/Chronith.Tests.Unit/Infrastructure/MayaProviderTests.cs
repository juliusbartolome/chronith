using Chronith.Application.DTOs;
using Chronith.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class MayaProviderTests
{
    [Fact]
    public void ProviderName_IsMaya()
    {
        var provider = CreateProvider();
        provider.ProviderName.Should().Be("Maya");
    }

    // ── ValidateWebhook (IP whitelisting) ────────────────────────────────────

    [Fact]
    public void ValidateWebhook_WithAllowedIp_ReturnsTrue()
    {
        var provider = CreateProvider(allowedIps: ["10.0.0.1", "10.0.0.2"]);

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: "10.0.0.1");

        provider.ValidateWebhook(context).Should().BeTrue();
    }

    [Fact]
    public void ValidateWebhook_WithDisallowedIp_ReturnsFalse()
    {
        var provider = CreateProvider(allowedIps: ["10.0.0.1"]);

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: "192.168.1.1");

        provider.ValidateWebhook(context).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhook_WithNullIp_ReturnsFalse()
    {
        var provider = CreateProvider(allowedIps: ["10.0.0.1"]);

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: null);

        provider.ValidateWebhook(context).Should().BeFalse();
    }

    [Fact]
    public void ValidateWebhook_WithEmptyAllowList_ReturnsFalse()
    {
        var provider = CreateProvider(allowedIps: []);

        var context = new WebhookValidationContext(
            Headers: new Dictionary<string, string>(),
            RawBody: "{}",
            SourceIpAddress: "10.0.0.1");

        provider.ValidateWebhook(context).Should().BeFalse();
    }

    // ── ParseWebhookPayload ──────────────────────────────────────────────────

    [Fact]
    public void ParseWebhookPayload_PaymentSuccess_ExtractsId()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_abc123",
            "paymentStatus": "PAYMENT_SUCCESS",
            "receiptNumber": "RN-123",
            "requestReferenceNumber": "booking-id-here"
        }
        """;

        var result = provider.ParseWebhookPayload(body);

        result.ProviderTransactionId.Should().Be("pay_abc123");
        result.EventType.Should().Be(PaymentEventType.Success);
    }

    [Fact]
    public void ParseWebhookPayload_PaymentFailed_MapsCorrectly()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_def456",
            "paymentStatus": "PAYMENT_FAILED"
        }
        """;

        var result = provider.ParseWebhookPayload(body);
        result.ProviderTransactionId.Should().Be("pay_def456");
        result.EventType.Should().Be(PaymentEventType.Failed);
    }

    [Fact]
    public void ParseWebhookPayload_PaymentExpired_MapsToExpired()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_ghi789",
            "paymentStatus": "PAYMENT_EXPIRED"
        }
        """;

        var result = provider.ParseWebhookPayload(body);
        result.ProviderTransactionId.Should().Be("pay_ghi789");
        result.EventType.Should().Be(PaymentEventType.Expired);
    }

    [Fact]
    public void ParseWebhookPayload_PaymentCancelled_MapsToCancelled()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_jkl012",
            "paymentStatus": "PAYMENT_CANCELLED"
        }
        """;

        var result = provider.ParseWebhookPayload(body);
        result.ProviderTransactionId.Should().Be("pay_jkl012");
        result.EventType.Should().Be(PaymentEventType.Cancelled);
    }

    // ── Legacy API (ValidateWebhookSignature, ParsePaymentEvent) ─────────────

    [Fact]
    public void ValidateWebhookSignature_AlwaysReturnsFalse()
    {
        // Maya doesn't use HMAC signatures — legacy method returns false
        var provider = CreateProvider();
        provider.ValidateWebhookSignature("{}", "any-header").Should().BeFalse();
    }

    [Fact]
    public void ParsePaymentEvent_PaymentSuccess_ReturnsPaidTrue()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_abc123",
            "paymentStatus": "PAYMENT_SUCCESS",
            "requestReferenceNumber": "booking-id"
        }
        """;

        var result = provider.ParsePaymentEvent(body);
        result.ExternalId.Should().Be("pay_abc123");
        result.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void ParsePaymentEvent_PaymentFailed_ReturnsPaidFalse()
    {
        var provider = CreateProvider();
        var body = """
        {
            "id": "pay_def456",
            "paymentStatus": "PAYMENT_FAILED"
        }
        """;

        var result = provider.ParsePaymentEvent(body);
        result.ExternalId.Should().Be("pay_def456");
        result.IsPaid.Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MayaProvider CreateProvider(string[]? allowedIps = null)
    {
        var options = Options.Create(new MayaOptions
        {
            PublicApiKey = "pk-test-key",
            SecretApiKey = "sk-test-key",
            BaseUrl = "https://pg-sandbox.paymaya.com",
            AllowedWebhookIps = allowedIps ?? ["13.229.160.234", "3.1.199.75"],
            SuccessUrl = "https://example.com/{bookingId}/success",
            FailureUrl = "https://example.com/{bookingId}/failed"
        });
        return new MayaProvider(options, null!);
    }
}
