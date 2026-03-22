using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
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
        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/bookings", new
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
        var response = await client.PostAsJsonAsync($"/v1/booking-types/{BookingTypeSlug}/bookings", new { });
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
        var response = await client.GetAsync($"/v1/booking-types/{BookingTypeSlug}/bookings");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListBookings_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/booking-types/{BookingTypeSlug}/bookings");
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
        var response = await client.GetAsync($"/v1/bookings/{bookingId}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/cancel — Admin, Staff, Customer → 200; PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CancelBooking_PaymentSvc_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/cancel", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CancelBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/cancel", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/confirm — Admin, Staff → 200; Customer, PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ConfirmBooking_NonStaff_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/confirm", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/confirm", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/pay — Admin, Staff, PaymentSvc → 200; Customer → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task PayBooking_Customer_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/pay", new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task PayBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/pay", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /bookings/{id} — TenantAdmin → 204; Staff, Customer, PaymentSvc → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task DeleteBooking_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync($"/v1/bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DeleteBooking_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"/v1/bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBooking_WithApiKey_WithDeleteScope_Returns204()
    {
        var btId = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var bookingId = await SeedData.SeedBookingAsync(db, btId,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"delete-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsDelete }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"/v1/bookings/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBooking_WithApiKey_WithoutDeleteScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"read-only-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"/v1/bookings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBooking_WithApiKey_WithReadScope_Returns200()
    {
        var btId = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var bookingId = await SeedData.SeedBookingAsync(db, btId,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"read-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync($"/v1/bookings/{bookingId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListBookings_WithApiKey_WithReadScope_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"read-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync($"/v1/booking-types/{BookingTypeSlug}/bookings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBooking_WithApiKey_WithoutReadScope_Returns403()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"wrong-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingsWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync($"/v1/bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
