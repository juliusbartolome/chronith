using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Tests.Functional.CustomerAuth;

[Collection("Functional")]
public sealed class CustomerMagicLinkEndpointsTests(FunctionalTestFixture fixture)
{
    private const string TenantSlug = "cust-magic-link";
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000012");
    private static readonly Guid CustomerId = Guid.Parse("10000000-0000-0000-0000-999000000012");
    private const string CustomerEmail = "magic-customer-12@example.com";

    /// <summary>
    /// No-op email channel — swallows sends so tests don't need a real SMTP server.
    /// </summary>
    private sealed class NullEmailChannel : INotificationChannel
    {
        public string ChannelType => "email";
        public Task SendAsync(NotificationMessage message, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// Creates an anonymous HTTP client with the real SMTP channel replaced by a no-op stub.
    /// Required because CustomerMagicLinkRegisterCommand sends an email after creating the customer.
    /// </summary>
    private HttpClient CreateClientWithNullEmail()
    {
        var factory = fixture.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureServices(services =>
            {
                // Remove all real INotificationChannel registrations
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(INotificationChannel))
                    .ToList();
                foreach (var d in descriptors)
                    services.Remove(d);

                // Register no-op email channel only
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
        // SeedCustomerAsync is not idempotent — guard against duplicate PK across parallel tests
        var exists = db.Customers.IgnoreQueryFilters()
            .Any(c => c.Id == CustomerId);
        if (!exists)
            await SeedData.SeedCustomerAsync(db, id: CustomerId, email: CustomerEmail, tenantId: TenantId);
    }

    [Fact]
    public async Task MagicLinkRegister_WithValidData_Returns202()
    {
        await EnsureSeedAsync();
        var client = CreateClientWithNullEmail();
        var email = $"magic-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/register", new
        {
            email,
            name = "Magic User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.ReadFromApiJsonAsync<MagicLinkInitiatedDto>();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MagicLinkRegister_WithDuplicateEmail_Returns409()
    {
        await EnsureSeedAsync();
        var client = CreateClientWithNullEmail();

        // CustomerEmail is already seeded — duplicate
        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/register", new
        {
            email = CustomerEmail,
            name = "Duplicate"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MagicLinkRegister_WithInvalidSlug_Returns404()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/public/nonexistent-magic/auth/magic-link/register", new
        {
            email = "nobody@example.com",
            name = "Nobody"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MagicLinkVerify_WithValidToken_Returns200WithTokens()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();
        var token = CreateMagicLinkToken(CustomerId, CustomerEmail, TenantSlug);

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/verify", new
        {
            token
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<CustomerAuthTokenDto>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        body.Customer.Email.Should().Be(CustomerEmail);
    }

    [Fact]
    public async Task MagicLinkVerify_WithInvalidToken_Returns401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync($"/v1/public/{TenantSlug}/auth/magic-link/verify", new
        {
            token = "this.is.not.a.valid.jwt"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string CreateMagicLinkToken(Guid customerId, string email, string tenantSlug)
    {
        // Use the same signing key as the test app so the token passes validation
        var key = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var creds = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
            new Claim("email", email),
            new Claim("tenantSlug", tenantSlug),
            new Claim("purpose", "magic-link-verify"),
        };
        var jwtToken = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }
}

