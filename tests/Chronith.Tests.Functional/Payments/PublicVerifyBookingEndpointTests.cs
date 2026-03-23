using System.Net;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Payments;

/// <summary>
/// Functional tests for the GET /v1/public/{tenantSlug}/bookings/{bookingId}/verify endpoint.
/// Validates HMAC signature enforcement and booking data retrieval.
/// </summary>
[Collection("Functional")]
public sealed class PublicVerifyBookingEndpointTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "verify-endpoint-type";
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
    public async Task GetVerifyBooking_WithValidHmac_Returns200WithBookingDetails()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(20);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000,
            checkoutUrl: "https://stub-checkout.local/test123");

        var (expires, sig) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/verify?expires={expires}&sig={sig}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(bookingId);
        dto.Status.Should().Be(BookingStatus.PendingPayment);
        dto.AmountInCentavos.Should().Be(50_000);
        dto.Currency.Should().Be("PHP");
        dto.CheckoutUrl.Should().Be("https://stub-checkout.local/test123",
            "PendingPayment bookings should expose the checkout URL");
    }

    [Fact]
    public async Task GetVerifyBooking_PendingVerification_ReturnsNullCheckoutUrl()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(21);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 50_000,
            checkoutUrl: "https://stub-checkout.local/should-not-show");

        var (expires, sig) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/verify?expires={expires}&sig={sig}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.PendingVerification);
        dto.CheckoutUrl.Should().BeNull(
            "non-PendingPayment bookings should not expose the checkout URL");
    }

    [Fact]
    public async Task GetVerifyBooking_WithInvalidSig_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(22);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000);

        var (expires, _) = GenerateHmacParams(bookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/verify?expires={expires}&sig=bad-signature");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVerifyBooking_NonexistentBooking_Returns404()
    {
        await EnsureSeedAsync();

        var fakeBookingId = Guid.NewGuid();
        var (expires, sig) = GenerateHmacParams(fakeBookingId, TenantSlug);
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{fakeBookingId}/verify?expires={expires}&sig={sig}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
