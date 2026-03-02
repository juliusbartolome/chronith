using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.BookingTypes;

[Collection("Functional")]
public sealed class BookingTypeAuthTests(FunctionalTestFixture fixture)
{
    private const string BaseUrl = "/booking-types";
    private const string Slug = "auth-test-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, Slug);
    }

    // GET /booking-types — TenantAdmin, TenantStaff, Customer → 200; TenantPaymentService → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.OK)]
    [InlineData("Customer", HttpStatusCode.OK)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListBookingTypes_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListBookingTypes_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /booking-types — TenantAdmin → 201; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CreateBookingType_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync(BaseUrl, new
        {
            slug = "should-not-create",
            name = "Test",
            isTimeSlot = true,
            capacity = 1,
            durationMinutes = 60
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CreateBookingType_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(BaseUrl, new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /booking-types/{slug} — TenantAdmin, TenantStaff, Customer → 200; TenantPaymentService → 403; anon → 401
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.OK)]
    [InlineData("TenantStaff", HttpStatusCode.OK)]
    [InlineData("Customer", HttpStatusCode.OK)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetBookingType_ReturnsExpectedStatus(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"{BaseUrl}/{Slug}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetBookingType_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"{BaseUrl}/{Slug}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /booking-types/{slug} — TenantAdmin → 200; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task UpdateBookingType_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PutAsJsonAsync($"{BaseUrl}/{Slug}", new { name = "New Name" });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateBookingType_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync($"{BaseUrl}/{Slug}", new { name = "New Name" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /booking-types/{slug} — TenantAdmin → 204; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task DeleteBookingType_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync($"{BaseUrl}/will-not-delete");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DeleteBookingType_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"{BaseUrl}/will-not-delete");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
