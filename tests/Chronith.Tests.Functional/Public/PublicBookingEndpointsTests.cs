using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Public;

[Collection("Functional")]
public sealed class PublicBookingEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "public-endpoints-type";
    private const string AutoPaidSlug = "public-auto-paid-type";
    private const string TenantSlug = "test-tenant";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        return await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    private async Task EnsureAutoPaidSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db,
            slug: AutoPaidSlug,
            capacity: 10,
            priceInCentavos: 50_000,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "Stub");
    }

    [Fact]
    public async Task PublicListBookingTypes_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/booking-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadFromApiJsonAsync<List<BookingTypeDto>>();
        types.Should().NotBeNull();
        types!.Should().Contain(bt => bt.Slug == BookingTypeSlug);
    }

    [Fact]
    public async Task PublicGetBookingType_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bt = await response.ReadFromApiJsonAsync<BookingTypeDto>();
        bt.Should().NotBeNull();
        bt!.Slug.Should().Be(BookingTypeSlug);
    }

    [Fact]
    public async Task PublicGetAvailability_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var from = Uri.EscapeDataString("2026-04-06T00:00:00Z");
        var to = Uri.EscapeDataString("2026-04-07T00:00:00Z");
        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}/availability?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var availability = await response.ReadFromApiJsonAsync<AvailabilityDto>();
        availability.Should().NotBeNull();
        availability!.Slots.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PublicCreateBooking_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var payload = new
        {
            StartTime = "2026-04-07T10:00:00Z",
            CustomerEmail = "public-customer@example.com",
            CustomerId = "public-cust-1"
        };

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.ReadFromApiJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.CustomerEmail.Should().Be("public-customer@example.com");
    }

    [Fact]
    public async Task PublicCreateBooking_WithInvalidTenantSlug_Returns404()
    {
        var client = fixture.CreateAnonymousClient();

        var payload = new
        {
            StartTime = "2026-04-07T10:00:00Z",
            CustomerEmail = "test@example.com",
            CustomerId = "cust-1"
        };

        var response = await client.PostAsJsonAsync(
            "/v1/public/nonexistent-tenant/booking-types/some-type/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicListStaff_Returns200()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedStaffMemberAsync(db, "Public Staff", "public-staff@example.com");

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/staff");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var staff = await response.ReadFromApiJsonAsync<List<StaffMemberDto>>();
        staff.Should().NotBeNull();
        staff!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PublicJoinWaitlist_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var payload = new
        {
            CustomerId = "public-waitlist-cust-1",
            CustomerEmail = "waitlist@example.com",
            DesiredStart = "2026-04-07T10:00:00Z",
            DesiredEnd = "2026-04-07T11:00:00Z"
        };

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}/waitlist", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = await response.ReadFromApiJsonAsync<WaitlistEntryDto>();
        entry.Should().NotBeNull();
        entry!.CustomerId.Should().Be("public-waitlist-cust-1");
    }

    // ── Public booking with Automatic payment mode: paymentUrl must be non-null ──

    [Fact]
    public async Task PublicCreateBooking_AutomaticPaid_ReturnsPaymentUrl()
    {
        await EnsureAutoPaidSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var payload = new
        {
            StartTime = "2026-04-08T10:00:00Z",
            CustomerEmail = $"pub-auto-{Guid.NewGuid():N}@example.com",
            CustomerId = "pub-auto-cust-1"
        };

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/booking-types/{AutoPaidSlug}/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.ReadFromApiJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment,
            "paid automatic bookings should start in PendingPayment status");
        booking.AmountInCentavos.Should().Be(50_000);
        booking.PaymentUrl.Should().NotBeNullOrEmpty(
            "public endpoint with Automatic mode + paid booking type must return an HMAC-signed payment URL");
        booking.PaymentUrl.Should().StartWith("https://test.example.com/pay",
            "payment URL should use the configured PaymentPage:BaseUrl");
        booking.CheckoutUrl.Should().BeNull(
            "checkout is deferred to the on-demand endpoint; no checkout URL at creation");
    }

    [Fact]
    public async Task PublicCreateBooking_ManualPaidMode_ReturnsPaymentUrl()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var payload = new
        {
            StartTime = "2026-04-08T11:00:00Z",
            CustomerEmail = $"pub-manual-{Guid.NewGuid():N}@example.com",
            CustomerId = "pub-manual-cust-1"
        };

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.ReadFromApiJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.PaymentUrl.Should().NotBeNullOrEmpty(
            "any paid booking should get a payment URL regardless of payment mode");
        booking.PaymentUrl.Should().StartWith("https://test.example.com/pay",
            "payment URL should use the configured PaymentPage:BaseUrl");
    }
}
