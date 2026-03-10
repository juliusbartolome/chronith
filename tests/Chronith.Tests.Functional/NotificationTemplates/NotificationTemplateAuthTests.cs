using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.NotificationTemplates;

[Collection("Functional")]
public sealed class NotificationTemplateAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "notif-template-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    // GET /tenant/notification-templates — Admin only; all others forbidden/unauth
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetAll_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/tenant/notification-templates");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetAll_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/notification-templates");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // GET /tenant/notification-templates/{id}
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task GetById_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync($"/v1/tenant/notification-templates/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task GetById_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/tenant/notification-templates/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /tenant/notification-templates/{id}
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task Update_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/notification-templates/{Guid.NewGuid()}",
            new { subject = "S", body = "B", isActive = true });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Update_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/notification-templates/{Guid.NewGuid()}",
            new { subject = "S", body = "B", isActive = true });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /tenant/notification-templates/{id}/preview
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task Preview_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync(
            $"/v1/tenant/notification-templates/{Guid.NewGuid()}/preview",
            new { variables = new Dictionary<string, string>() });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Preview_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/tenant/notification-templates/{Guid.NewGuid()}/preview",
            new { variables = new Dictionary<string, string>() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /tenant/notification-templates/reset/{eventType}
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task Reset_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsync(
            "/v1/tenant/notification-templates/reset/booking.confirmed", null);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Reset_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsync(
            "/v1/tenant/notification-templates/reset/booking.confirmed", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
