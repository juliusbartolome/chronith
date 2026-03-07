using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Payments;

/// <summary>
/// Functional tests verifying the end-to-end payment flow behaviour
/// for different payment modes and pricing configurations.
/// </summary>
[Collection("Functional")]
public sealed class PaymentFlowTests(FunctionalTestFixture fixture)
{
    // ── Slugs unique to this test class to avoid collision with other tests ──

    private const string FreeBookingSlug = "payment-flow-free";
    private const string AutoStubSlug = "payment-flow-auto-stub";
    private const string ManualSlug = "payment-flow-manual";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        // Free booking type: price=0, Automatic mode (should skip payment → PendingVerification)
        await SeedData.SeedBookingTypeAsync(db,
            slug: FreeBookingSlug,
            capacity: 10,
            priceInCentavos: 0,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "Stub");

        // Paid booking type: Automatic + Stub provider (should create checkout session)
        await SeedData.SeedBookingTypeAsync(db,
            slug: AutoStubSlug,
            capacity: 10,
            priceInCentavos: 50_000,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "Stub");

        // Manual payment mode: paid but no automatic checkout
        await SeedData.SeedBookingTypeAsync(db,
            slug: ManualSlug,
            capacity: 10,
            priceInCentavos: 50_000,
            paymentMode: PaymentMode.Manual);
    }

    // ── Free booking: price=0 → PendingVerification (skips PendingPayment) ──

    [Fact]
    public async Task CreateBooking_FreeBookingType_StatusIsPendingVerification()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{FreeBookingSlug}/bookings",
            new
            {
                startTime = "2026-08-01T09:00:00Z",
                customerEmail = $"free-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingVerification,
            "free bookings (price=0) should skip PendingPayment and go directly to PendingVerification");
        booking.AmountInCentavos.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_FreeBookingType_NoCheckoutUrl()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{FreeBookingSlug}/bookings",
            new
            {
                startTime = "2026-08-01T10:00:00Z",
                customerEmail = $"free-nocheckout-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.CheckoutUrl.Should().BeNull(
            "free bookings should not have a checkout URL since no payment is needed");
    }

    // ── Paid + Automatic + Stub: creates checkout session ──

    [Fact]
    public async Task CreateBooking_AutomaticWithStub_StatusIsPendingPayment()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{AutoStubSlug}/bookings",
            new
            {
                startTime = "2026-08-02T09:00:00Z",
                customerEmail = $"auto-stub-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment,
            "paid automatic bookings should start in PendingPayment status");
        booking.AmountInCentavos.Should().Be(50_000);
    }

    [Fact]
    public async Task CreateBooking_AutomaticWithStub_ReturnsCheckoutUrl()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{AutoStubSlug}/bookings",
            new
            {
                startTime = "2026-08-02T10:00:00Z",
                customerEmail = $"auto-checkout-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.CheckoutUrl.Should().NotBeNullOrEmpty(
            "Automatic mode with Stub provider should return a checkout URL");
        booking.CheckoutUrl.Should().StartWith("https://stub-checkout.local/",
            "the checkout URL should come from the Stub payment provider");
    }

    [Fact]
    public async Task CreateBooking_AutomaticWithStub_HasPaymentReference()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{AutoStubSlug}/bookings",
            new
            {
                startTime = "2026-08-02T11:00:00Z",
                customerEmail = $"auto-ref-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.PaymentReference.Should().NotBeNullOrEmpty(
            "Automatic mode with Stub provider should set a payment reference (provider transaction ID)");
        booking.PaymentReference.Should().StartWith("stub_",
            "Stub provider transaction IDs start with 'stub_'");
    }

    // ── Manual mode: no automatic checkout ──

    [Fact]
    public async Task CreateBooking_ManualMode_StatusIsPendingPayment()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{ManualSlug}/bookings",
            new
            {
                startTime = "2026-08-03T09:00:00Z",
                customerEmail = $"manual-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment,
            "manual mode bookings with non-zero price should start in PendingPayment");
    }

    [Fact]
    public async Task CreateBooking_ManualMode_NoCheckoutUrl()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync(
            $"/booking-types/{ManualSlug}/bookings",
            new
            {
                startTime = "2026-08-03T10:00:00Z",
                customerEmail = $"manual-nocheckout-{Guid.NewGuid():N}@example.com"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.CheckoutUrl.Should().BeNull(
            "manual payment mode should not create an automatic checkout session");
        booking.PaymentReference.Should().BeNull(
            "manual payment mode should not set a payment reference automatically");
    }
}
