using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Payments;

[Collection("Functional")]
public sealed class PaymentWebhookAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    private static object ValidPayload(Guid bookingId) => new
    {
        bookingId,
        paymentReference = "ref-test"
    };

    // POST /webhooks/payment — PaymentSvc → processes (200 or 404 depending on booking); others → 403; anon → 401

    [Fact]
    public async Task PaymentWebhook_PaymentSvc_DoesNotReturn403Or401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantPaymentService");

        // Use a non-existent booking ID — we expect a domain error (400/404/422), NOT an auth error
        var response = await client.PostAsJsonAsync("/webhooks/payment", ValidPayload(Guid.NewGuid()));
        ((int)response.StatusCode).Should().NotBe(403);
        ((int)response.StatusCode).Should().NotBe(401);
    }

    [Theory]
    [InlineData("TenantAdmin", HttpStatusCode.Forbidden)]
    [InlineData("TenantStaff", HttpStatusCode.Forbidden)]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    public async Task PaymentWebhook_NonPaymentSvc_ReturnsForbidden(string role, HttpStatusCode expected)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync("/webhooks/payment", ValidPayload(Guid.NewGuid()));
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task PaymentWebhook_Anonymous_Returns401()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync("/webhooks/payment", ValidPayload(Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
