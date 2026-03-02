using System.Net;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class WebhookAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "webhook-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    private string ListUrl => $"/booking-types/{BookingTypeSlug}/webhooks";
    private string CreateUrl => $"/booking-types/{BookingTypeSlug}/webhooks";
    private string DeleteUrl(Guid id) => $"/booking-types/{BookingTypeSlug}/webhooks/{id}";

    // GET /booking-types/{slug}/webhooks — Admin → 200; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListWebhooks_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync(ListUrl);
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListWebhooks_Admin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync(ListUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListWebhooks_Anonymous_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync(ListUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // POST /booking-types/{slug}/webhooks — Admin → 201; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task CreateWebhook_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync(CreateUrl, new { url = "https://example.com", secret = "s" });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task CreateWebhook_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(CreateUrl, new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /booking-types/{slug}/webhooks/{id} — Admin → 204; others → 403; anon → 401
    [Theory]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task DeleteWebhook_NonAdmin_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync(DeleteUrl(Guid.NewGuid()));
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DeleteWebhook_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync(DeleteUrl(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
