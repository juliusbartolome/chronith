using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Tenant;

[Collection("Functional")]
public sealed class TenantSettingsEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "settings-test-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task GetTenantSettings_AsAdmin_ReturnsDefaults()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/tenant/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<TenantSettingsDto>();
        dto.Should().NotBeNull();
        dto!.TenantId.Should().Be(TestConstants.TenantId);
        dto.PrimaryColor.Should().Be("#2563EB");
        dto.BookingPageEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTenantSettings_AsAdmin_PersistsChanges()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var payload = new
        {
            PrimaryColor = "#FF5733",
            AccentColor = "#33FF57",
            WelcomeMessage = "Welcome to our booking page!",
            BookingPageEnabled = true
        };

        var putResponse = await client.PutAsJsonAsync("/v1/tenant/settings", payload);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await putResponse.ReadFromApiJsonAsync<TenantSettingsDto>();
        dto.Should().NotBeNull();
        dto!.PrimaryColor.Should().Be("#FF5733");
        dto.AccentColor.Should().Be("#33FF57");
        dto.WelcomeMessage.Should().Be("Welcome to our booking page!");
        dto.BookingPageEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTenantSettings_InvalidHexColor_Returns400()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var payload = new { PrimaryColor = "not-a-color" };

        var response = await client.PutAsJsonAsync("/v1/tenant/settings", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
