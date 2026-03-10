using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.NotificationTemplates;

[Collection("Functional")]
public sealed class NotificationTemplateEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "notif-template-endpoints-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task GetAll_AsAdmin_Returns200WithTemplates()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.get-all",
            channelType: "email");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync("/v1/tenant/notification-templates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await response.ReadFromApiJsonAsync<List<NotificationTemplateDto>>();
        templates.Should().NotBeNull();
        templates!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var id = await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.get-by-id",
            channelType: "email",
            subject: "Get By Id Subject",
            body: "Get by id body.");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"/v1/tenant/notification-templates/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var template = await response.ReadFromApiJsonAsync<NotificationTemplateDto>();
        template.Should().NotBeNull();
        template!.Id.Should().Be(id);
        template.EventType.Should().Be("booking.confirmed.get-by-id");
        template.Subject.Should().Be("Get By Id Subject");
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync($"/v1/tenant/notification-templates/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsAdmin_Returns200WithUpdatedData()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var id = await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.update",
            channelType: "email",
            subject: "Original Subject",
            body: "Original body.");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/notification-templates/{id}",
            new
            {
                subject = "Updated Subject",
                body = "Updated body text.",
                isActive = false
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadFromApiJsonAsync<NotificationTemplateDto>();
        updated.Should().NotBeNull();
        updated!.Subject.Should().Be("Updated Subject");
        updated.Body.Should().Be("Updated body text.");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_WithEmptyBody_Returns400()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var id = await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.update-invalid",
            channelType: "email");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/notification-templates/{id}",
            new { subject = (string?)null, body = "", isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Preview_AsAdmin_ReturnsSubstitutedContent()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var id = await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.preview",
            channelType: "email",
            subject: "Hi {{CustomerName}}",
            body: "Your booking {{BookingId}} is confirmed.");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsJsonAsync(
            $"/v1/tenant/notification-templates/{id}/preview",
            new
            {
                variables = new Dictionary<string, string>
                {
                    { "CustomerName", "Alice" },
                    { "BookingId", "B-001" }
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await response.ReadFromApiJsonAsync<PreviewDto>();
        preview.Should().NotBeNull();
        preview!.Subject.Should().Be("Hi Alice");
        preview.Body.Should().Be("Your booking B-001 is confirmed.");
    }

    [Fact]
    public async Task Preview_WhenTemplateNotFound_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsJsonAsync(
            $"/v1/tenant/notification-templates/{Guid.NewGuid()}/preview",
            new { variables = new Dictionary<string, string>() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reset_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedNotificationTemplateAsync(db,
            eventType: "booking.confirmed.reset",
            channelType: "email");

        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.PostAsync(
            "/v1/tenant/notification-templates/reset/booking.confirmed.reset", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Helper record to deserialize preview response
    private sealed record PreviewDto(string? Subject, string Body);
}
