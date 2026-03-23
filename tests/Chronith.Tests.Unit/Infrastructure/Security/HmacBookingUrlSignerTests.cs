using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public sealed class HmacBookingUrlSignerTests
{
    // 32 bytes of 0x01 — valid HMAC key
    private static readonly string ValidKey =
        Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());

    private static IBookingUrlSigner CreateSigner(int lifetimeSeconds = 3600)
    {
        var hmacOptions = Options.Create(new BlindIndexOptions { HmacKey = ValidKey });
        var pageOptions = Options.Create(new PaymentPageOptions
        {
            TokenLifetimeSeconds = lifetimeSeconds
        });
        return new HmacBookingUrlSigner(hmacOptions, pageOptions);
    }

    [Fact]
    public void GenerateSignedUrl_ReturnsUrlWithAllParams()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, "test-tenant");

        url.Should().StartWith("https://app.com/pay?");
        url.Should().Contain($"bookingId={bookingId}");
        url.Should().Contain("tenantSlug=test-tenant");
        url.Should().Contain("expires=");
        url.Should().Contain("sig=");
    }

    [Fact]
    public void GenerateSignedUrl_ExpiresIsInFuture()
    {
        var signer = CreateSigner();
        var url = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var query = new Uri(url).Query;
        var expires = long.Parse(
            System.Web.HttpUtility.ParseQueryString(query)["expires"]!);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        expires.Should().BeGreaterThan(now);
        expires.Should().BeLessThanOrEqualTo(now + 3600 + 5); // 1h + small tolerance
    }

    [Fact]
    public void GenerateSignedUrl_SignatureIs64CharHex()
    {
        var signer = CreateSigner();
        var url = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var sig = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["sig"]!;
        sig.Should().HaveLength(64);
        sig.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Validate_WithValidSignature_ReturnsTrue()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(bookingId, tenantSlug, expires, sig).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithExpiredToken_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        // Generate a URL, then extract sig — but pretend expires was in the past
        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var sig = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["sig"]!;
        var pastExpires = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();

        // sig was computed for a future expires, so this will fail both expiry AND sig check
        signer.Validate(bookingId, tenantSlug, pastExpires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedBookingId_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(Guid.NewGuid(), tenantSlug, expires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedTenantSlug_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, "tenant-a");
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(bookingId, "tenant-b", expires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedSignature_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);

        signer.Validate(bookingId, tenantSlug, expires, "deadbeef" + new string('0', 56))
            .Should().BeFalse();
    }

    [Fact]
    public void DifferentBookings_ProduceDifferentSignatures()
    {
        var signer = CreateSigner();
        var url1 = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");
        var url2 = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var sig1 = System.Web.HttpUtility.ParseQueryString(new Uri(url1).Query)["sig"]!;
        var sig2 = System.Web.HttpUtility.ParseQueryString(new Uri(url2).Query)["sig"]!;
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void Constructor_InvalidKey_Throws()
    {
        var hmacOptions = Options.Create(new BlindIndexOptions { HmacKey = "" });
        var pageOptions = Options.Create(new PaymentPageOptions());
        var act = () => new HmacBookingUrlSigner(hmacOptions, pageOptions);
        act.Should().Throw<InvalidOperationException>();
    }
}
