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
/// Verifies that CheckoutUrl is persisted to the database after POST /bookings
/// so that a subsequent GET /bookings/{id} returns the same non-null value.
/// Regression test for the data-loss bug where CheckoutUrl was set in memory
/// but not written to the bookings table.
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
    public async Task CreateBooking_WithAutomaticPaymentMode_CheckoutUrlPersistedAfterGet()
    {
        await EnsureSeedAsync();

        var client = fixture.CreateClient("Customer");

        // POST — creates booking; handler calls StubPaymentProvider and sets CheckoutUrl in memory
        var createResp = await client.PostAsJsonAsync(
            $"/v1/booking-types/{AutoPayBookingTypeSlug}/bookings",
            new
            {
                startTime = "2026-07-15T09:00:00Z",
                customerEmail = $"checkout-url-{Guid.NewGuid():N}@example.com"
            });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<BookingDto>();
        created.Should().NotBeNull();

        // The POST response itself should already have CheckoutUrl
        created!.CheckoutUrl.Should().NotBeNullOrEmpty(
            "the create response should include the checkout URL from the payment provider");

        // GET — loads the booking fresh from the database
        var getResp = await client.GetAsync($"/v1/bookings/{created.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<BookingDto>();
        fetched.Should().NotBeNull();

        // This is the regression assertion: CheckoutUrl must survive the round-trip to the DB
        fetched!.CheckoutUrl.Should().NotBeNullOrEmpty(
            "CheckoutUrl must be persisted to the database and returned on GET");
        fetched.CheckoutUrl.Should().Be(created.CheckoutUrl,
            "the persisted CheckoutUrl must match the one returned at creation time");
    }
}
