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
    private const string TenantSlug = "test-tenant";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        return await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task PublicListBookingTypes_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/public/{TenantSlug}/booking-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<BookingTypeDto>>();
        types.Should().NotBeNull();
        types!.Should().Contain(bt => bt.Slug == BookingTypeSlug);
    }

    [Fact]
    public async Task PublicGetBookingType_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync($"/public/{TenantSlug}/booking-types/{BookingTypeSlug}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bt = await response.Content.ReadFromJsonAsync<BookingTypeDto>();
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
            $"/public/{TenantSlug}/booking-types/{BookingTypeSlug}/availability?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var availability = await response.Content.ReadFromJsonAsync<AvailabilityDto>();
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
            $"/public/{TenantSlug}/booking-types/{BookingTypeSlug}/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
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
            "/public/nonexistent-tenant/booking-types/some-type/bookings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicListStaff_Returns200()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedStaffMemberAsync(db, "Public Staff", "public-staff@example.com");

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/public/{TenantSlug}/staff");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var staff = await response.Content.ReadFromJsonAsync<List<StaffMemberDto>>();
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
            $"/public/{TenantSlug}/booking-types/{BookingTypeSlug}/waitlist", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = await response.Content.ReadFromJsonAsync<WaitlistEntryDto>();
        entry.Should().NotBeNull();
        entry!.CustomerId.Should().Be("public-waitlist-cust-1");
    }
}
