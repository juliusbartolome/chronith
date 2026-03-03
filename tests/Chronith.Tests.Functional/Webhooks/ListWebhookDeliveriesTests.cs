using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Webhooks;

[Collection("Functional")]
public sealed class ListWebhookDeliveriesTests(FunctionalTestFixture fixture)
{
    private const string Slug = "list-deliveries-bt";

    [Fact]
    public async Task GetDeliveries_AsAdmin_Returns200WithPagedResults()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);
        await SeedData.SeedOutboxEntriesAsync(db, webhookId, count: 3);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"/webhooks/{webhookId}/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<WebhookDeliveryDto>>();
        body!.TotalCount.Should().Be(3);
        body.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDeliveries_AsStaff_Returns200()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);

        var client = fixture.CreateClient("TenantStaff");
        var response = await client.GetAsync($"/webhooks/{webhookId}/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDeliveries_AsCustomer_Returns403()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);

        var client = fixture.CreateClient("Customer");
        var response = await client.GetAsync($"/webhooks/{webhookId}/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDeliveries_ForNonExistentWebhook_Returns404()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"/webhooks/{Guid.NewGuid()}/deliveries");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDeliveries_PaginationWorks()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, Slug);
        var webhookId = await SeedData.SeedWebhookAsync(db, bookingTypeId);
        await SeedData.SeedOutboxEntriesAsync(db, webhookId, count: 25);

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"/webhooks/{webhookId}/deliveries?page=2&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResultDto<WebhookDeliveryDto>>();
        body!.TotalCount.Should().BeGreaterThanOrEqualTo(25);
        body.Items.Should().HaveCount(10);
    }
}
