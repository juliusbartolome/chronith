using System.Net;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Integrations;

[Collection("Functional")]
public sealed class ICalFeedTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "ical-feed-type";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var btId = await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);

        // Seed a confirmed booking so the feed has a VEVENT
        var start = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(1);
        await SeedData.SeedBookingAsync(db, btId, start, end, BookingStatus.Confirmed, "ical-cust-1");

        return btId;
    }

    [Fact]
    public async Task ICalFeed_ReturnsValidICalendar()
    {
        await EnsureSeedAsync();

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/booking-types/{BookingTypeSlug}/calendar.ics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("BEGIN:VCALENDAR");
        body.Should().Contain("VERSION:2.0");
        body.Should().Contain("PRODID:-//Chronith//Booking Engine//EN");
        body.Should().Contain("BEGIN:VEVENT");
        body.Should().Contain("DTSTART:20260610T090000Z");
        body.Should().Contain("DTEND:20260610T100000Z");
        body.Should().Contain("SUMMARY:Booking - Test Type");
        body.Should().Contain("END:VEVENT");
        body.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public async Task ICalFeed_WithUnknownSlug_Returns404()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/booking-types/nonexistent-slug/calendar.ics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ICalFeed_WithNoConfirmedBookings_ReturnsEmptyCalendar()
    {
        // Seed a booking type with only cancelled bookings
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        const string slug = "ical-empty-type";
        var btId = await SeedData.SeedBookingTypeAsync(db, slug);

        var start = new DateTimeOffset(2026, 6, 11, 9, 0, 0, TimeSpan.Zero);
        await SeedData.SeedBookingAsync(db, btId, start, start.AddHours(1),
            BookingStatus.Cancelled, "ical-cust-empty");

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/booking-types/{slug}/calendar.ics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("BEGIN:VCALENDAR");
        body.Should().NotContain("BEGIN:VEVENT");
    }
}
