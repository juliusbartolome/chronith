using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Bookings;

[Collection("Functional")]
public sealed class BookingRescheduleTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "reschedule-test-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task Reschedule_ConfirmedBooking_Returns200WithUpdatedTimes()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Seed a confirmed booking
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var btId = (await db.BookingTypes.FirstAsync(bt => bt.Slug == BookingTypeSlug)).Id;
        var originalStart = DateTimeOffset.UtcNow.AddDays(10);
        var bookingId = await SeedData.SeedBookingAsync(
            db, btId, originalStart, originalStart.AddHours(1), BookingStatus.Confirmed);

        // Reschedule
        var newStart = DateTimeOffset.UtcNow.AddDays(12);
        var newEnd = newStart.AddHours(1);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{bookingId}/reschedule", new
        {
            newStart,
            newEnd
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Start.Should().BeCloseTo(newStart, TimeSpan.FromSeconds(1));
        booking.End.Should().BeCloseTo(newEnd, TimeSpan.FromSeconds(1));
        booking.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Reschedule_CancelledBooking_Returns422()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Seed a cancelled booking
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var btId = (await db.BookingTypes.FirstAsync(bt => bt.Slug == BookingTypeSlug)).Id;
        var start = DateTimeOffset.UtcNow.AddDays(11);
        var bookingId = await SeedData.SeedBookingAsync(
            db, btId, start, start.AddHours(1), BookingStatus.Cancelled);

        // Attempt reschedule
        var newStart = DateTimeOffset.UtcNow.AddDays(14);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{bookingId}/reschedule", new
        {
            newStart,
            newEnd = newStart.AddHours(1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reschedule_AsCustomer_OwnBooking_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        // Seed a confirmed booking for the customer
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var btId = (await db.BookingTypes.FirstAsync(bt => bt.Slug == BookingTypeSlug)).Id;
        var start = DateTimeOffset.UtcNow.AddDays(15);
        var bookingId = await SeedData.SeedBookingAsync(
            db, btId, start, start.AddHours(1), BookingStatus.Confirmed,
            customerId: TestConstants.CustomerUserId);

        // Reschedule own booking
        var newStart = DateTimeOffset.UtcNow.AddDays(16);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{bookingId}/reschedule", new
        {
            newStart,
            newEnd = newStart.AddHours(1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
