using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerAuthAccessTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-auth-access";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000006");

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: TenantId, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, tenantId: TenantId);
    }

    // GET /public/{slug}/auth/me — Customer-only
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetMe_NonCustomerRole_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role, tenantId: TenantId);
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/auth/me");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetMe_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /public/{slug}/auth/me — Customer-only
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task UpdateMe_NonCustomerRole_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role, tenantId: TenantId);
        var response = await client.PutAsJsonAsync($"/v1/public/{TenantSlug}/auth/me", new
        {
            name = "Test", phone = (string?)null
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateMe_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync($"/v1/public/{TenantSlug}/auth/me", new
        {
            name = "Test", phone = (string?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /public/{slug}/my-bookings — Customer-only
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetMyBookings_NonCustomerRole_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role, tenantId: TenantId);
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetMyBookings_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /public/{slug}/my-bookings/{id} — Customer-only
    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetMyBookingDetail_NonCustomerRole_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role, tenantId: TenantId);
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetMyBookingDetail_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/my-bookings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
