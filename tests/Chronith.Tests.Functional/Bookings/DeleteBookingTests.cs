using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Bookings;

[Collection("Functional")]
public sealed class DeleteBookingTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "delete-booking-type";

    private async Task<(Guid BookingTypeId, Guid BookingId)> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var btId = await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug, capacity: 10, durationMinutes: 60);
        var bookingId = await SeedData.SeedBookingAsync(
            db, btId,
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(1).AddHours(1));
        return (btId, bookingId);
    }

    [Fact]
    public async Task DeleteBooking_AsAdmin_Returns204()
    {
        var (_, bookingId) = await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.DeleteAsync($"/v1/bookings/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBooking_AsAdmin_BookingNoLongerAccessible()
    {
        var (_, bookingId) = await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        await client.DeleteAsync($"/v1/bookings/{bookingId}");

        // After soft delete, GET should return 404
        var getResponse = await client.GetAsync($"/v1/bookings/{bookingId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBooking_WhenNotFound_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.DeleteAsync($"/v1/bookings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
