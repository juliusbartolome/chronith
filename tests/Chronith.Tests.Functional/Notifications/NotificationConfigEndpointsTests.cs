using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Notifications;

[Collection("Functional")]
public sealed class NotificationConfigEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "notif-config-endpoints-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task UpdateNotificationConfig_AsAdmin_Creates_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PutAsJsonAsync("/v1/tenant/notifications/email", new
        {
            settings = "{\"smtpHost\":\"smtp.example.com\",\"smtpPort\":587}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.ReadFromApiJsonAsync<TenantNotificationConfigDto>();
        config.Should().NotBeNull();
        config!.ChannelType.Should().Be("email");
        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotificationConfig_AsAdmin_Upserts_ExistingConfig()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create initial config
        var createResp = await client.PutAsJsonAsync("/v1/tenant/notifications/sms", new
        {
            settings = "{\"accountSid\":\"AC000\",\"authToken\":\"tok\"}"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResp.ReadFromApiJsonAsync<TenantNotificationConfigDto>();

        // Update same channel
        var updateResp = await client.PutAsJsonAsync("/v1/tenant/notifications/sms", new
        {
            settings = "{\"accountSid\":\"AC111\",\"authToken\":\"tok2\"}"
        });
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.ReadFromApiJsonAsync<TenantNotificationConfigDto>();

        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created!.Id);
        updated.ChannelType.Should().Be("sms");
        updated.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ListNotificationConfigs_AsAdmin_ReturnsConfigs()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Ensure at least one config exists
        await client.PutAsJsonAsync("/v1/tenant/notifications/push", new
        {
            settings = "{\"projectId\":\"my-project\"}"
        });

        var response = await client.GetAsync("/v1/tenant/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var configs = await response.ReadFromApiJsonAsync<List<TenantNotificationConfigDto>>();
        configs.Should().NotBeNull();
        configs!.Should().Contain(c => c.ChannelType == "push");
    }

    [Fact]
    public async Task DisableNotificationChannel_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create config first
        await client.PutAsJsonAsync("/v1/tenant/notifications/email", new
        {
            settings = "{\"smtpHost\":\"smtp.example.com\"}"
        });

        // Disable
        var deleteResp = await client.DeleteAsync("/v1/tenant/notifications/email");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's disabled (still in list but IsEnabled = false)
        var listResp = await client.GetAsync("/v1/tenant/notifications");
        var configs = await listResp.ReadFromApiJsonAsync<List<TenantNotificationConfigDto>>();
        var emailConfig = configs!.FirstOrDefault(c => c.ChannelType == "email");
        emailConfig.Should().NotBeNull();
        emailConfig!.IsEnabled.Should().BeFalse();
    }
}
