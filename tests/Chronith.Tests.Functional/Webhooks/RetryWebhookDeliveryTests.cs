using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class RetryWebhookDeliveryTests(FunctionalTestFixture fixture)
{
    private const string Slug = "retry-delivery-bt";

    [Fact]
    public async Task RetryDelivery_WithFailedEntry_Returns204()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);
        var deliveryId = await SeedData.SeedFailedOutboxEntryAsync(db, webhookId);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsync(
            $"/v1/webhooks/{webhookId}/deliveries/{deliveryId}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it was reset to Pending
        var getResponse = await client.GetAsync($"/v1/webhooks/{webhookId}/deliveries");
        var body = await getResponse.ReadFromApiJsonAsync<PagedResultDto<WebhookDeliveryDto>>();
        body!.Items.First(e => e.Id == deliveryId).Status.Should().Be(OutboxStatus.Pending);
    }

    [Fact]
    public async Task RetryDelivery_WithPendingEntry_Returns422()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);
        var entries = await SeedData.SeedOutboxEntriesAsync(db, webhookId, count: 1);
        var deliveryId = entries[0];

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsync(
            $"/v1/webhooks/{webhookId}/deliveries/{deliveryId}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RetryDelivery_AsStaff_Returns403()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);
        var deliveryId = await SeedData.SeedFailedOutboxEntryAsync(db, webhookId);

        var client = fixture.CreateClient("TenantStaff");
        var response = await client.PostAsync(
            $"/v1/webhooks/{webhookId}/deliveries/{deliveryId}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RetryDelivery_ForNonExistentDelivery_Returns404()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsync(
            $"/v1/webhooks/{webhookId}/deliveries/{Guid.NewGuid()}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
