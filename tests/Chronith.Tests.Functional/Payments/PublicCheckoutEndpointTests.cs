using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Payments;

/// <summary>
/// Functional tests for the POST /v1/public/{tenantSlug}/bookings/{bookingId}/checkout endpoint.
/// Validates HMAC signature enforcement and on-demand checkout session creation.
/// </summary>
[Collection("Functional")]
public sealed class PublicCheckoutEndpointTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "checkout-endpoint-type";
    private const string TenantSlug = "test-tenant";

    private async Task<(Guid TenantId, Guid BookingTypeId)> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var tenantId = await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db,
            slug: BookingTypeSlug,
            capacity: 10,
            priceInCentavos: 50_000,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "Stub");
        return (tenantId, bookingTypeId);
    }

    private (long Expires, string Sig) GenerateHmacParams(Guid bookingId, string tenantSlug)
    {
        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var url = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, tenantSlug);
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return (long.Parse(query["expires"]!), query["sig"]!);
    }

    [Fact]
    public async Task PostCheckout_WithValidHmac_Returns200WithCheckoutUrl()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(10);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000);

        var (expires, sig) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/checkout?expires={expires}&sig={sig}",
            new { providerName = "Stub" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadFromApiJsonAsync<CreateCheckoutResult>();
        result.Should().NotBeNull();
        result!.CheckoutUrl.Should().StartWith("https://stub-checkout.local/");
        result.ProviderTransactionId.Should().StartWith("stub_");
    }

    [Fact]
    public async Task PostCheckout_WithInvalidSig_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(11);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000);

        var (expires, _) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/checkout?expires={expires}&sig=invalid-signature",
            new { providerName = "Stub" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCheckout_WithExpiredToken_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(12);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000);

        // Generate valid params then use an expired timestamp
        var (_, sig) = GenerateHmacParams(bookingId, TenantSlug);
        long expiredTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/checkout?expires={expiredTimestamp}&sig={sig}",
            new { providerName = "Stub" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostCheckout_ForNonPendingPaymentBooking_Returns422()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(13);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.Confirmed,
            amountInCentavos: 50_000);

        var (expires, sig) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/checkout?expires={expires}&sig={sig}",
            new { providerName = "Stub" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "InvalidStateTransitionException maps to 422 via DomainException handler");
    }

    [Fact]
    public async Task PostCheckout_WithUnknownProvider_Returns404()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(14);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000);

        var (expires, sig) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/checkout?expires={expires}&sig={sig}",
            new { providerName = "NonExistentProvider" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unknown provider name should result in NotFoundException");
    }
}
