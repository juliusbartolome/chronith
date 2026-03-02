using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class WebhookEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "webhook-endpoints-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    private string WebhooksUrl => $"/booking-types/{BookingTypeSlug}/webhooks";
    private string WebhookUrl(Guid id) => $"/booking-types/{BookingTypeSlug}/webhooks/{id}";

    [Fact]
    public async Task CreateWebhook_AsAdmin_Returns201WithId()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = "https://example.com/hook",
            secret = "super-secret-1234"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await response.Content.ReadFromJsonAsync<WebhookDto>();
        webhook.Should().NotBeNull();
        webhook!.Id.Should().NotBeEmpty();
        webhook.Url.Should().Be("https://example.com/hook");
    }

    [Fact]
    public async Task ListWebhooks_AsAdmin_ReturnsCreatedWebhook()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a webhook first
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = $"https://example.com/list-hook-{Guid.NewGuid():N}",
            secret = "test-secret-at-least-16chars"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<WebhookDto>();

        // List and verify it appears
        var listResp = await client.GetAsync(WebhooksUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var webhooks = await listResp.Content.ReadFromJsonAsync<List<WebhookDto>>();
        webhooks.Should().NotBeNull();
        webhooks!.Should().Contain(w => w.Id == created!.Id);
    }

    [Fact]
    public async Task DeleteWebhook_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a webhook to delete
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = $"https://example.com/delete-hook-{Guid.NewGuid():N}",
            secret = "secret-for-delete-test"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<WebhookDto>();

        // Delete it
        var deleteResp = await client.DeleteAsync(WebhookUrl(created!.Id));
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateListDelete_HappyPath()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var hookUrl = $"https://example.com/full-flow-{Guid.NewGuid():N}";

        // Create
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new { url = hookUrl, secret = "secret-full-flow-ok" });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await createResp.Content.ReadFromJsonAsync<WebhookDto>();
        webhook.Should().NotBeNull();

        // List — should contain the new webhook
        var listResp = await client.GetAsync(WebhooksUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<WebhookDto>>();
        list!.Should().Contain(w => w.Id == webhook!.Id);

        // Delete
        var deleteResp = await client.DeleteAsync(WebhookUrl(webhook!.Id));
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
