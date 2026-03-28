using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
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

    private string WebhooksUrl => $"/v1/booking-types/{BookingTypeSlug}/webhooks";
    private string WebhookUrl(Guid id) => $"/v1/booking-types/{BookingTypeSlug}/webhooks/{id}";

    [Fact]
    public async Task CreateWebhook_AsAdmin_Returns201WithId()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = "https://example.com/hook",
            secret = "super-secret-1234",
            eventTypes = new[] { WebhookEventTypes.Confirmed, WebhookEventTypes.Cancelled }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await response.ReadFromApiJsonAsync<WebhookDto>();
        webhook.Should().NotBeNull();
        webhook!.Id.Should().NotBeEmpty();
        webhook.Url.Should().Be("https://example.com/hook");
        webhook.EventTypes.Should().BeEquivalentTo(
            [WebhookEventTypes.Confirmed, WebhookEventTypes.Cancelled]);
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
            secret = "test-secret-at-least-16chars",
            eventTypes = new[] { WebhookEventTypes.PaymentReceived }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<WebhookDto>();

        // List and verify it appears
        var listResp = await client.GetAsync(WebhooksUrl);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var webhooks = await listResp.ReadFromApiJsonAsync<List<WebhookDto>>();
        webhooks.Should().NotBeNull();
        var match = webhooks!.FirstOrDefault(w => w.Id == created!.Id);
        match.Should().NotBeNull();
        match!.EventTypes.Should().Contain(WebhookEventTypes.PaymentReceived);
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
            secret = "secret-for-delete-test",
            eventTypes = new[] { WebhookEventTypes.Confirmed }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<WebhookDto>();

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
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = hookUrl,
            secret = "secret-full-flow-ok",
            eventTypes = new[]
            {
                WebhookEventTypes.Confirmed,
                WebhookEventTypes.Cancelled,
                WebhookEventTypes.PaymentReceived,
                WebhookEventTypes.PaymentFailed
            }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await createResp.ReadFromApiJsonAsync<WebhookDto>();
        webhook.Should().NotBeNull();
        webhook!.EventTypes.Should().HaveCount(4);

        // List — should contain the new webhook
        var listResp = await client.GetAsync(WebhooksUrl);
        var list = await listResp.ReadFromApiJsonAsync<List<WebhookDto>>();
        list!.Should().Contain(w => w.Id == webhook.Id);

        // Delete
        var deleteResp = await client.DeleteAsync(WebhookUrl(webhook.Id));
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Event type specific tests ─────────────────────────────────────────────

    [Fact]
    public async Task CreateWebhook_WithEventTypes_Returns201WithEventTypes()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = $"https://example.com/evt-{Guid.NewGuid():N}",
            secret = "secret-event-types-test",
            eventTypes = new[]
            {
                WebhookEventTypes.PaymentReceived,
                WebhookEventTypes.Confirmed
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var webhook = await response.ReadFromApiJsonAsync<WebhookDto>();
        webhook.Should().NotBeNull();
        webhook!.EventTypes.Should().HaveCount(2);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.PaymentReceived);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.Confirmed);
    }

    [Fact]
    public async Task CreateWebhook_WithInvalidEventType_Returns400()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = "https://example.com/bad-event",
            secret = "secret-invalid-event!",
            eventTypes = new[] { "booking.nonexistent" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateWebhook_WithEmptyEventTypes_Returns400()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = "https://example.com/empty-events",
            secret = "secret-empty-event-test",
            eventTypes = Array.Empty<string>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Update endpoint tests ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateWebhook_AsAdmin_Returns200WithUpdatedFields()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a webhook first
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = $"https://example.com/update-test-{Guid.NewGuid():N}",
            secret = "secret-for-update-test",
            eventTypes = new[] { WebhookEventTypes.Confirmed }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<WebhookDto>();

        // Update URL via PATCH
        var newUrl = $"https://example.com/updated-{Guid.NewGuid():N}";
        var updateResp = await client.PatchAsJsonAsync(WebhookUrl(created!.Id), new
        {
            url = newUrl
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.ReadFromApiJsonAsync<WebhookDto>();
        updated.Should().NotBeNull();
        updated!.Url.Should().Be(newUrl);
        // Event types should be preserved when not provided in update
        updated.EventTypes.Should().Contain(WebhookEventTypes.Confirmed);
    }

    [Fact]
    public async Task UpdateWebhook_UpdateEventTypes_PersistsCorrectly()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a webhook with one event type
        var createResp = await client.PostAsJsonAsync(WebhooksUrl, new
        {
            url = $"https://example.com/evt-update-{Guid.NewGuid():N}",
            secret = "secret-for-evt-update-test",
            eventTypes = new[] { WebhookEventTypes.Confirmed }
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.ReadFromApiJsonAsync<WebhookDto>();
        created!.EventTypes.Should().HaveCount(1);

        // Update to different event types
        var updateResp = await client.PatchAsJsonAsync(WebhookUrl(created.Id), new
        {
            eventTypes = new[]
            {
                WebhookEventTypes.PaymentReceived,
                WebhookEventTypes.Cancelled,
                WebhookEventTypes.PaymentFailed
            }
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.ReadFromApiJsonAsync<WebhookDto>();
        updated.Should().NotBeNull();
        updated!.EventTypes.Should().HaveCount(3);
        updated.EventTypes.Should().BeEquivalentTo(
        [
            WebhookEventTypes.PaymentReceived,
            WebhookEventTypes.Cancelled,
            WebhookEventTypes.PaymentFailed
        ]);

        // Verify via list that the change persisted
        var listResp = await client.GetAsync(WebhooksUrl);
        var list = await listResp.ReadFromApiJsonAsync<List<WebhookDto>>();
        var fromList = list!.First(w => w.Id == created.Id);
        fromList.EventTypes.Should().HaveCount(3);
        fromList.EventTypes.Should().NotContain(WebhookEventTypes.Confirmed);
    }
}
