using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.TenantPaymentConfig;

[Collection("Functional")]
public sealed class TenantPaymentConfigEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "payment-config-endpoints-type";
    private const string TenantSlug = "test-tenant";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    [Fact]
    public async Task CreatePaymentConfig_AsAdmin_Returns200WithDto()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var payload = new
        {
            ProviderName = "Manual",
            Label = "GCash",
            Settings = "{}",
            PublicNote = "Scan to pay via GCash",
            QrCodeUrl = "https://qr.example.com/gcash"
        };

        var response = await client.PostAsJsonAsync("/v1/tenant/payment-config", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        dto.Should().NotBeNull();
        dto!.ProviderName.Should().Be("Manual");
        dto.Label.Should().Be("GCash");
        dto.IsActive.Should().BeTrue(); // Manual is always active on create
        dto.PublicNote.Should().Be("Scan to pay via GCash");
    }

    [Fact]
    public async Task GetPaymentConfigs_AsAdmin_ReturnsListIncludingCreated()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create one first
        var payload = new
        {
            ProviderName = "Manual",
            Label = "BankTransfer-List-Test",
            Settings = "{}",
            PublicNote = default(string),
            QrCodeUrl = default(string)
        };

        await client.PostAsJsonAsync("/v1/tenant/payment-config", payload);

        var response = await client.GetAsync("/v1/tenant/payment-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.ReadFromApiJsonAsync<List<TenantPaymentConfigDto>>();
        list.Should().NotBeNull();
        list!.Should().Contain(c => c.Label == "BankTransfer-List-Test");
    }

    [Fact]
    public async Task UpdatePaymentConfig_AsAdmin_Returns200WithUpdatedDto()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var createPayload = new
        {
            ProviderName = "Manual",
            Label = "Cash-Update-Test",
            Settings = "{}",
            PublicNote = default(string),
            QrCodeUrl = default(string)
        };
        var createResponse = await client.PostAsJsonAsync("/v1/tenant/payment-config", createPayload);
        var created = await createResponse.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        created.Should().NotBeNull();

        // Update
        var updatePayload = new
        {
            Label = "Cash-Updated",
            Settings = "{}",
            PublicNote = "Pay in cash",
            QrCodeUrl = default(string)
        };
        var updateResponse = await client.PutAsJsonAsync($"/v1/tenant/payment-config/{created!.Id}", updatePayload);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        updated.Should().NotBeNull();
        updated!.Label.Should().Be("Cash-Updated");
        updated.PublicNote.Should().Be("Pay in cash");
    }

    [Fact]
    public async Task ActivatePaymentConfig_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a PayMongo config (starts inactive)
        var createPayload = new
        {
            ProviderName = "PayMongo",
            Label = "PayMongo-Activate-Test",
            Settings = """{"SecretKey":"sk_test_abc","PublicKey":"pk_test_abc","WebhookSecret":"ws","SuccessUrl":"https://example.com/success","FailureUrl":"https://example.com/failure"}""",
            PublicNote = default(string),
            QrCodeUrl = default(string)
        };
        var createResponse = await client.PostAsJsonAsync("/v1/tenant/payment-config", createPayload);
        var created = await createResponse.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        created.Should().NotBeNull();
        created!.IsActive.Should().BeFalse();

        var activateResponse = await client.PatchAsync(
            $"/v1/tenant/payment-config/{created.Id}/activate", null);

        activateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatePaymentConfig_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create Manual (starts active) then deactivate
        var createPayload = new
        {
            ProviderName = "Manual",
            Label = "Cash-Deactivate-Test",
            Settings = "{}",
            PublicNote = default(string),
            QrCodeUrl = default(string)
        };
        var createResponse = await client.PostAsJsonAsync("/v1/tenant/payment-config", createPayload);
        var created = await createResponse.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        created.Should().NotBeNull();

        var deactivateResponse = await client.PatchAsync(
            $"/v1/tenant/payment-config/{created!.Id}/deactivate", null);

        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletePaymentConfig_AsAdmin_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var createPayload = new
        {
            ProviderName = "Manual",
            Label = "Cash-Delete-Test",
            Settings = "{}",
            PublicNote = default(string),
            QrCodeUrl = default(string)
        };
        var createResponse = await client.PostAsJsonAsync("/v1/tenant/payment-config", createPayload);
        var created = await createResponse.ReadFromApiJsonAsync<TenantPaymentConfigDto>();
        created.Should().NotBeNull();

        var deleteResponse = await client.DeleteAsync($"/v1/tenant/payment-config/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm it no longer appears in list
        var listResponse = await client.GetAsync("/v1/tenant/payment-config");
        var list = await listResponse.ReadFromApiJsonAsync<List<TenantPaymentConfigDto>>();
        list.Should().NotBeNull();
        list!.Should().NotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task GetPublicPaymentProviders_Anonymous_Returns200WithActiveSummaries()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create an active Manual config
        var createPayload = new
        {
            ProviderName = "Manual",
            Label = "GCash-Public-Test",
            Settings = "{}",
            PublicNote = "Pay via GCash",
            QrCodeUrl = "https://qr.example.com/gcash-public"
        };
        await adminClient.PostAsJsonAsync("/v1/tenant/payment-config", createPayload);

        var anonClient = fixture.CreateAnonymousClient();
        var response = await anonClient.GetAsync($"/v1/public/{TenantSlug}/payment-providers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await response.ReadFromApiJsonAsync<List<PaymentProviderSummaryDto>>();
        summaries.Should().NotBeNull();
        summaries!.Should().Contain(s => s.Label == "GCash-Public-Test");
    }
}
