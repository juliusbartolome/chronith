using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.TenantPaymentConfig;

[Collection("Functional")]
public sealed class TenantPaymentConfigAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    private static readonly object CreatePayload = new
    {
        ProviderName = "Manual",
        Label = "Auth-Test-Config",
        Settings = "{}",
        PublicNote = default(string),
        QrCodeUrl = default(string)
    };

    // --- GET /tenant/payment-config ---

    [Fact]
    public async Task GetPaymentConfigs_Admin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");
        var response = await client.GetAsync("/v1/tenant/payment-config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task GetPaymentConfigs_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.GetAsync("/v1/tenant/payment-config");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentConfigs_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/tenant/payment-config");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /tenant/payment-config ---

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task CreatePaymentConfig_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync("/v1/tenant/payment-config", CreatePayload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePaymentConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/v1/tenant/payment-config", CreatePayload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- PUT /tenant/payment-config/{id} ---

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task UpdatePaymentConfig_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}", CreatePayload);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdatePaymentConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PutAsJsonAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}", CreatePayload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- DELETE /tenant/payment-config/{id} ---

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task DeletePaymentConfig_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.DeleteAsync($"/v1/tenant/payment-config/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeletePaymentConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.DeleteAsync($"/v1/tenant/payment-config/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- PATCH /tenant/payment-config/{id}/activate ---

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task ActivatePaymentConfig_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PatchAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}/activate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActivatePaymentConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PatchAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}/activate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- PATCH /tenant/payment-config/{id}/deactivate ---

    [Theory]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    public async Task DeactivatePaymentConfig_NonAdmin_ReturnsForbidden(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PatchAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivatePaymentConfig_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PatchAsync(
            $"/v1/tenant/payment-config/{Guid.NewGuid()}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /public/{tenantSlug}/payment-providers (public - anonymous allowed) ---

    [Fact]
    public async Task GetPublicPaymentProviders_Anonymous_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/v1/public/test-tenant/payment-providers");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
