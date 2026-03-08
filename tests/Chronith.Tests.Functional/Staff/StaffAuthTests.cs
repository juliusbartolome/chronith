using System.Net;
using System.Net.Http.Json;
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

        var response = await client.PostAsJsonAsync("/staff", new
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

        var response = await client.PostAsJsonAsync("/staff", new
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
        var response = await client.GetAsync("/staff");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/staff");
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
        var response = await client.GetAsync($"/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/staff/{Guid.NewGuid()}");
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
        var response = await client.PutAsJsonAsync($"/staff/{Guid.NewGuid()}", new
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
        var response = await client.PutAsJsonAsync($"/staff/{Guid.NewGuid()}", new
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
        var response = await client.DeleteAsync($"/staff/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DeleteStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"/staff/{Guid.NewGuid()}");
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
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/assign-staff", new
        {
            staffMemberId = Guid.NewGuid()
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task AssignStaff_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/bookings/{Guid.NewGuid()}/assign-staff", new
        {
            staffMemberId = Guid.NewGuid()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
