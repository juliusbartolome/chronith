using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Bookings;

/// <summary>
/// Verifies that the HMAC payment URL flow correctly defers checkout session creation.
/// With the new HMAC-signed payment flow, CheckoutUrl is null at creation time and
/// PaymentUrl (HMAC-signed) is returned instead. CheckoutUrl is only populated after
/// the customer creates a checkout session on-demand via the public checkout endpoint.
/// </summary>
[Collection("Functional")]
public sealed class BookingCheckoutUrlPersistenceTests(FunctionalTestFixture fixture)
{
    private const string AutoPayBookingTypeSlug = "auto-pay-checkout-url-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        // Idempotent — only seed if not yet present
        var existing = db.BookingTypes.IgnoreQueryFilters()
            .FirstOrDefault(bt => bt.Slug == AutoPayBookingTypeSlug
                                  && bt.TenantId == TestConstants.TenantId);
        if (existing is not null) return;

        var bookingTypeId = Guid.NewGuid();
        db.BookingTypes.Add(new BookingTypeEntity
        {
            Id = bookingTypeId,
            TenantId = TestConstants.TenantId,
            Slug = AutoPayBookingTypeSlug,
            Name = "Auto Pay Type",
            Kind = BookingKind.TimeSlot,
            Capacity = 10,
            PaymentMode = PaymentMode.Automatic,
            PaymentProvider = "Stub",
            IsDeleted = false,
            DurationMinutes = 60,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0,
            PriceInCentavos = 10_000,
            Currency = "PHP"
        });

        // Availability windows for all days (08:00–18:00) so any test time resolves
        foreach (var dow in Enum.GetValues<DayOfWeek>())
        {
            db.AvailabilityWindows.Add(new AvailabilityWindowEntity
            {
                Id = Guid.NewGuid(),
                BookingTypeId = bookingTypeId,
                DayOfWeek = (int)dow,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0)
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBooking_WithAutomaticPaymentMode_ReturnsPaymentUrlNotCheckoutUrl()
    {
        await EnsureSeedAsync();

        var client = fixture.CreateClient("Customer");

        // POST — creates booking; handler now generates HMAC payment URL instead of checkout session
        var createResp = await client.PostAsJsonAsync(
            $"/v1/booking-types/{AutoPayBookingTypeSlug}/bookings",
            new
            {
                startTime = "2026-07-15T09:00:00Z",
                customerEmail = $"checkout-url-{Guid.NewGuid():N}@example.com"
            });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<BookingDto>();
        created.Should().NotBeNull();

        // CheckoutUrl should be null — checkout is now deferred to the on-demand endpoint
        created!.CheckoutUrl.Should().BeNull(
            "checkout session creation is deferred; CheckoutUrl is null until on-demand checkout");

        // PaymentUrl should be an HMAC-signed URL pointing to the payment selection page
        created.PaymentUrl.Should().NotBeNullOrEmpty(
            "Automatic mode should return an HMAC-signed PaymentUrl for the payment selection page");
        created.PaymentUrl.Should().StartWith("https://test.example.com/pay",
            "the payment URL should use the configured PaymentPage:BaseUrl");

        // GET — verify the booking round-trips correctly
        var getResp = await client.GetAsync($"/v1/bookings/{created.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.ReadFromApiJsonAsync<BookingDto>();
        fetched.Should().NotBeNull();

        // CheckoutUrl remains null on GET (no checkout session created yet)
        fetched!.CheckoutUrl.Should().BeNull(
            "CheckoutUrl should remain null until on-demand checkout is triggered");
    }
}
