using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Bookings;

[Collection("Functional")]
public sealed class BookingLifecycleTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "lifecycle-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug, capacity: 10, durationMinutes: 60);
    }

    [Fact]
    public async Task CreateBooking_AsCustomer_Returns201()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = "2026-06-10T09:00:00Z",
            customerEmail = $"lifecycle-{Guid.NewGuid():N}@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        booking!.Status.Should().Be(BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task CreateThenConfirm_AsStaff_TransitionsToConfirmed()
    {
        await EnsureSeedAsync();

        // Step 1: Create booking as Customer
        var customerClient = fixture.CreateClient("Customer");
        var createResp = await customerClient.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = "2026-06-11T10:00:00Z",
            customerEmail = $"lifecycle2-{Guid.NewGuid():N}@example.com"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await createResp.Content.ReadFromJsonAsync<BookingDto>();
        booking.Should().NotBeNull();

        // Step 2: Confirm as Staff (requires PendingVerification — first pay to move to PendingVerification)
        // Per domain: PendingPayment → pay → PendingVerification → confirm → Confirmed
        var staffClient = fixture.CreateClient("TenantStaff");
        var payResp = await staffClient.PostAsJsonAsync($"/bookings/{booking!.Id}/pay", new
        {
            bookingTypeSlug = BookingTypeSlug
        });
        payResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var paidBooking = await payResp.Content.ReadFromJsonAsync<BookingDto>();
        paidBooking!.Status.Should().Be(BookingStatus.PendingVerification);

        var confirmResp = await staffClient.PostAsJsonAsync($"/bookings/{booking.Id}/confirm", new
        {
            bookingTypeSlug = BookingTypeSlug
        });
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmedBooking = await confirmResp.Content.ReadFromJsonAsync<BookingDto>();
        confirmedBooking!.Status.Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task CreateThenCancel_AsCustomer_TransitionsToCancelled()
    {
        await EnsureSeedAsync();

        var client = fixture.CreateClient("Customer");
        var createResp = await client.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = "2026-06-12T14:00:00Z",
            customerEmail = $"lifecycle3-{Guid.NewGuid():N}@example.com"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await createResp.Content.ReadFromJsonAsync<BookingDto>();

        var cancelResp = await client.PostAsJsonAsync($"/bookings/{booking!.Id}/cancel", new
        {
            bookingTypeSlug = BookingTypeSlug
        });
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await cancelResp.Content.ReadFromJsonAsync<BookingDto>();
        cancelled!.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task GetBooking_AfterCreate_ReturnsCorrectId()
    {
        await EnsureSeedAsync();

        var client = fixture.CreateClient("TenantAdmin");
        var createResp = await client.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = "2026-06-13T08:00:00Z",
            customerEmail = $"get-check-{Guid.NewGuid():N}@example.com"
        });
        var booking = await createResp.Content.ReadFromJsonAsync<BookingDto>();

        var getResp = await client.GetAsync($"/bookings/{booking!.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResp.Content.ReadFromJsonAsync<BookingDto>();
        fetched!.Id.Should().Be(booking.Id);
    }
}
