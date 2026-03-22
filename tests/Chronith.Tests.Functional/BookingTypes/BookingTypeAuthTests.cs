using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.BookingTypes;

[Collection("Functional")]
public sealed class BookingTypeAuthTests(FunctionalTestFixture fixture)
{
    private const string BaseUrl = "/v1/booking-types";
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

    // --- API Key scope tests ---

    [Fact]
    public async Task ListBookingTypes_WithApiKey_WithReadScope_Returns200()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"bt-read-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingTypesRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListBookingTypes_WithApiKey_WithoutReadScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"bt-write-only-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingTypesWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateBookingType_WithApiKey_WithWriteScope_Returns201()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"bt-write-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingTypesWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var slug = $"api-key-bt-{Guid.NewGuid():N}";
        var response = await apiKeyClient.PostAsJsonAsync(BaseUrl, new
        {
            slug,
            name = "API Key Created BT",
            isTimeSlot = true,
            capacity = 1,
            durationMinutes = 60
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBookingType_WithApiKey_WithoutWriteScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"bt-read-only-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.BookingTypesRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.PostAsJsonAsync(BaseUrl, new
        {
            slug = $"should-not-create-{Guid.NewGuid():N}",
            name = "Should Not Create",
            isTimeSlot = true,
            capacity = 1,
            durationMinutes = 60
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
