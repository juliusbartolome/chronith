using System.Net;
using System.Text;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Payments;

/// <summary>
/// Verifies that the payment webhook endpoint at POST /webhooks/payments/{tenantId}/{provider}
/// is AllowAnonymous — external payment providers send webhooks without JWT tokens.
/// </summary>
[Collection("Functional")]
public sealed class PaymentWebhookAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    // The webhook endpoint reads raw body + headers, not JSON-bound request objects.
    // Send a minimal body so it reaches the handler (which will fail on validation,
    // not on auth). We use StringContent to match the raw-body approach.
    private static StringContent WebhookBody() =>
        new("{}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task PaymentWebhook_Anonymous_DoesNotReturn401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(401,
            "the webhook endpoint is AllowAnonymous and should not require authentication");
    }

    [Fact]
    public async Task PaymentWebhook_Anonymous_DoesNotReturn403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(403,
            "the webhook endpoint is AllowAnonymous and should not check roles");
    }

    [Theory]
    [InlineData("TenantAdmin")]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    [InlineData("TenantPaymentService")]
    public async Task PaymentWebhook_AnyRole_DoesNotReturn401Or403(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(401);
        ((int)response.StatusCode).Should().NotBe(403,
            $"role '{role}' should not be forbidden from calling the webhook endpoint");
    }

    [Fact]
    public async Task PaymentWebhook_UnknownProvider_Returns4xxError()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/UnknownProvider", WebhookBody());

        // Unknown/unconfigured providers return 401 by design (security: don't reveal which
        // providers are configured for a tenant). It should never be 403 (middleware auth rejection).
        ((int)response.StatusCode).Should().NotBe(403,
            "an unknown provider should not return a 403 Forbidden from auth middleware");
        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400,
            "an unknown provider should result in an error, not a success");
    }
}
