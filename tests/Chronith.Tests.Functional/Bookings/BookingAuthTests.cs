using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Bookings;

[Collection("Functional")]
public sealed class BookingAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "booking-auth-type";

    private async Task<Guid> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        return await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    // POST /booking-types/{slug}/bookings — Admin, Staff, Customer → 201; PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Created)]
    [InlineData("TenantStaff", HttpStatusCode.Created)]
    [InlineData("Customer", HttpStatusCode.Created)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CreateBooking_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new
        {
            startTime = $"2026-05-{10 + new Random().Next(0, 10):00}T09:00:00Z",
            customerEmail = $"{role.ToLower()}-{Guid.NewGuid():N}@example.com"
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CreateBooking_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/booking-types/{BookingTypeSlug}/bookings", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /booking-types/{slug}/bookings — Admin, Staff → 200; Customer, PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.OK)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListBookings_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/booking-types/{BookingTypeSlug}/bookings");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListBookings_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/booking-types/{BookingTypeSlug}/bookings");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /bookings/{id} — Admin, Staff, Customer → 200; PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.OK)]
    [InlineData("Customer", HttpStatusCode.OK)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetBooking_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        var btId = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var bookingId = await SeedData.SeedBookingAsync(db, btId,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/bookings/{bookingId}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/cancel — Admin, Staff, Customer → 200; PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CancelBooking_PaymentSvc_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/cancel", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CancelBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/cancel", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/confirm — Admin, Staff → 200; Customer, PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ConfirmBooking_NonStaff_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/confirm", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/confirm", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/pay — Admin, Staff, PaymentSvc → 200; Customer → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task PayBooking_Customer_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/pay", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task PayBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/pay", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
