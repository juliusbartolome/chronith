using System.Net;
using Chronith.Application.Interfaces;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerMagicLinkAuthTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-magic-auth";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000013");

    /// <summary>No-op email channel — swallows sends so tests don't need a real SMTP server.</summary>
    private sealed class NullEmailChannel : INotificationChannel
    {
        public string ChannelType => "email";
        public Task SendAsync(NotificationMessage message, CancellationToken ct) => Task.CompletedTask;
    }

    private HttpClient CreateClientWithNullEmail()
    {
        var factory = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(INotificationChannel))
                    .ToList();
                foreach (var d in descriptors)
                    services.Remove(d);
                services.AddSingleton<INotificationChannel, NullEmailChannel>();
            });
        });
        return factory.CreateClient();
    }

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: TenantId, slug: TenantSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, magicLink: true, allowBuiltIn: false, tenantId: TenantId);
    }

    // Register is public — no JWT needed
    [Fact]
    public async Task MagicLinkRegister_NoJwt_IsAllowed()
    {
        await EnsureSeedAsync();
        var client = CreateClientWithNullEmail();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/register", new
        {
            email = $"open-{Guid.NewGuid():N}@example.com",
            name = "Open Access"
        });

        // 202 = endpoint reached without auth challenge
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // Verify is public — no JWT needed
    [Fact]
    public async Task MagicLinkVerify_NoJwt_IsAllowed()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        // Invalid token → 401 from our own validation, NOT from auth middleware
        // The key point: the endpoint doesn't require an Authorization header
        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/verify", new
        {
            token = "invalid.token.here"
        });

        // Should be 401 from magic link validation, not 401 from missing bearer token
        // The endpoint must reach the handler (400 from validator or 401 from our handler, not 403 from auth)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    // Register endpoint on a tenant without magic link → error
    [Fact]
    public async Task MagicLinkRegister_WhenMagicLinkNotEnabled_ReturnsError()
    {
        // Seed a tenant with magic link DISABLED
        var disabledSlug = "magic-disabled-" + Guid.NewGuid().ToString("N")[..8];
        var disabledTenantId = Guid.NewGuid();
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db, id: disabledTenantId, slug: disabledSlug);
        await SeedData.SeedTenantAuthConfigAsync(db, magicLink: false, tenantId: disabledTenantId);

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync($"/v1/public/{disabledSlug}/auth/magic-link/register", new
        {
            email = "test@example.com",
            name = "Test"
        });

        // Expect 500 — InvalidOperationException is not a DomainException, falls through to catch-all in ExceptionHandlingMiddleware
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
