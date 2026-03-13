using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Signup;

[Collection("Functional")]
public sealed class SignupEndpointsTests(FunctionalTestFixture fixture)
{
    private static int _slugCounter = 100;

    // Generate a unique slug per test to avoid slug conflicts
    private static string UniqueSlug() =>
        $"signup-test-{System.Threading.Interlocked.Increment(ref _slugCounter)}";

    [Fact]
    public async Task Signup_WithValidData_Returns201WithResult()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = UniqueSlug();

        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "Test Org",
            tenantSlug = slug,
            timeZoneId = "UTC",
            email = $"{slug}@example.com",
            password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadFromApiJsonAsync<SignupResultDto>();
        body.Should().NotBeNull();
        body!.TenantId.Should().NotBeEmpty();
        body.AdminUserId.Should().NotBeEmpty();
        body.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Signup_DuplicateEmail_Returns409()
    {
        var client = fixture.CreateAnonymousClient();
        var slug = UniqueSlug();
        var email = $"{slug}@example.com";

        // First signup — should succeed
        await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "First Org",
            tenantSlug = slug,
            timeZoneId = "UTC",
            email,
            password = "SecurePass123!"
        });

        // Second signup with same email but different slug
        var secondSlug = UniqueSlug();
        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "Second Org",
            tenantSlug = secondSlug,
            timeZoneId = "UTC",
            email,
            password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Signup_InvalidEmail_Returns400()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "Bad Org",
            tenantSlug = UniqueSlug(),
            timeZoneId = "UTC",
            email = "not-an-email",
            password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Signup_InvalidSlug_Returns400()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "Bad Slug Org",
            tenantSlug = "Invalid Slug With Spaces!",
            timeZoneId = "UTC",
            email = "valid@example.com",
            password = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Signup_PasswordTooShort_Returns400()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "Short Pass Org",
            tenantSlug = UniqueSlug(),
            timeZoneId = "UTC",
            email = "valid@example.com",
            password = "short"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Signup_MissingFields_Returns400()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/v1/signup", new
        {
            tenantName = "",
            tenantSlug = "",
            timeZoneId = "",
            email = "",
            password = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
