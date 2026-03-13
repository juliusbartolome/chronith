using Chronith.Client.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Chronith.Client.Tests;

[Collection("SDK Integration")]
public sealed class ChronithClientIntegrationTests(SdkTestFixture fixture)
{
    [Fact]
    public async Task BookingTypes_List_ReturnsPagedResult()
    {
        var client = fixture.CreateChronithClient("TenantAdmin");

        var result = await client.BookingTypes.ListAsync();

        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PageSize.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task TenantSettings_Get_ReturnsDefaults()
    {
        await fixture.EnsureTenantSeededAsync();
        var client = fixture.CreateChronithClient("TenantAdmin");

        var settings = await client.Tenant.GetSettingsAsync();

        settings.Should().NotBeNull();
        settings.PrimaryColor.Should().Be("#2563EB");
        settings.BookingPageEnabled.Should().BeTrue();
        settings.TenantId.Should().Be(TestConstants.TenantId);
    }

    [Fact]
    public async Task Plans_List_ReturnsAllActivePlans()
    {
        var client = fixture.CreateChronithClient("TenantAdmin");

        var plans = await client.Tenant.GetPlansAsync();

        plans.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Staff_List_ReturnsPagedResult()
    {
        var client = fixture.CreateChronithClient("TenantAdmin");

        var result = await client.Staff.ListAsync();

        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task BookingTypes_GetNonExistent_ThrowsChronithApiException()
    {
        var client = fixture.CreateChronithClient("TenantAdmin");

        var act = async () => await client.BookingTypes.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Chronith.Client.Errors.ChronithApiException>()
            .Where(e => (int)e.StatusCode == 404);
    }

    [Fact]
    public async Task UnauthenticatedClient_GetSettings_ThrowsChronithApiException()
    {
        var client = fixture.CreateAnonymousChronithClient();

        var act = async () => await client.Tenant.GetSettingsAsync();

        await act.Should().ThrowAsync<Chronith.Client.Errors.ChronithApiException>()
            .Where(e => (int)e.StatusCode == 401);
    }
}
