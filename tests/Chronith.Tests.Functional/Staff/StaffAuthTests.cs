using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Staff;

[Collection("Functional")]
public sealed class StaffAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "staff-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    // POST /staff — Admin → 201; Customer → 403; anon → 401
    [Fact]
    public async Task CreateStaff_AsCustomer_Returns403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("Customer");

        var response = await client.PostAsJsonAsync("/v1/staff", new
        {
            name = "Forbidden Staff",
            email = "forbidden@example.com",
            availabilityWindows = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/staff", new
        {
            name = "Anon Staff",
            email = "anon@example.com",
            availabilityWindows = Array.Empty<object>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /staff — Admin/Staff → 200; Customer → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListStaff_NonAdminNonStaff_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/staff");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/staff");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /staff/{id} — Admin/Staff → 200; Customer → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetStaff_NonAdminNonStaff_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/v1/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /staff/{id} — Admin only; Staff → 403; Customer → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task UpdateStaff_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PutAsJsonAsync($"/v1/staff/{Guid.NewGuid()}", new
        {
            name = "Forbidden Update",
            email = "no@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync($"/v1/staff/{Guid.NewGuid()}", new
        {
            name = "Anon",
            email = "no@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /staff/{id} — Admin only
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task DeleteStaff_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync($"/v1/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DeleteStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"/v1/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /bookings/{id}/assign-staff — Admin/Staff → 200; Customer → 403; anon → 401
    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task AssignStaff_NonAdminNonStaff_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/assign-staff", new
        {
            staffMemberId = Guid.NewGuid()
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task AssignStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/v1/bookings/{Guid.NewGuid()}/assign-staff", new
        {
            staffMemberId = Guid.NewGuid()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- API Key scope tests ---

    [Fact]
    public async Task ListStaff_WithApiKey_WithStaffReadScope_Returns200()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"staff-read-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.StaffRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/staff");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListStaff_WithApiKey_WithoutStaffReadScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"wrong-scope-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.StaffWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.GetAsync("/v1/staff");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteStaff_WithApiKey_WithStaffWriteScope_Returns404OrNoContent()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"staff-write-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.StaffWrite }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        // Non-existent staff id: auth passes → 404 (not 401/403)
        var response = await apiKeyClient.DeleteAsync($"/v1/staff/{Guid.NewGuid()}");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteStaff_WithApiKey_WithoutStaffWriteScope_Returns403()
    {
        await EnsureSeedAsync();

        var adminClient = fixture.CreateClient("TenantAdmin");
        var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
        {
            description = $"read-only-staff-key-{Guid.NewGuid():N}",
            scopes = new[] { ApiKeyScope.StaffRead }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

        var apiKeyClient = fixture.CreateAnonymousClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

        var response = await apiKeyClient.DeleteAsync($"/v1/staff/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
