using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Notifications;

[Collection("Functional")]
public sealed class NotificationAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "notif-auth-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    // GET /tenant/notifications — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task ListNotificationConfigs_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/tenant/notifications");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ListNotificationConfigs_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // PUT /tenant/notifications/{channelType} — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task UpdateNotificationConfig_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PutAsJsonAsync("/v1/tenant/notifications/email", new
        {
            settings = "{\"smtpHost\":\"smtp.example.com\"}"
        });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateNotificationConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync("/v1/tenant/notifications/email", new
        {
            settings = "{\"smtpHost\":\"smtp.example.com\"}"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // DELETE /tenant/notifications/{channelType} — Admin only

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("TenantPaymentService", HttpStatusCode.Forbidden)]
    public async Task DisableNotificationChannel_NonAdmin_ReturnsForbidden(
        string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync("/v1/tenant/notifications/email");
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task DisableNotificationChannel_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync("/v1/tenant/notifications/email");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
