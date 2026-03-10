using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Security;

[Collection("Functional")]
public sealed class RateLimitTests(FunctionalTestFixture fixture)
{
    // Auth policy: 10 requests per 300 seconds, partitioned by IP.
    // In WebApplicationFactory all requests share the same virtual IP, so
    // sending 11 requests exhausts the 10-permit window and the 11th gets 429.
    private const string AuthLoginPath = "/v1/auth/login";
    private const int AuthPolicyPermitLimit = 10; // matches RateLimitingOptions.Auth default

    private static readonly object InvalidLoginPayload = new
    {
        tenantSlug = "does-not-exist",
        email = "nobody@nowhere.test",
        password = "wrong-password"
    };

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_Returns429()
    {
        var client = fixture.CreateAnonymousClient();

        HttpResponseMessage? lastResponse = null;

        // Send permit-limit + 1 requests.
        // Requests 1..10 will return 400 or 404 (bad credentials / unknown tenant).
        // Request 11 must return 429 — the rate limiter rejects before the handler runs.
        for (int i = 0; i <= AuthPolicyPermitLimit; i++)
        {
            lastResponse = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_ResponseHasRetryAfterHeader()
    {
        var client = fixture.CreateAnonymousClient();

        HttpResponseMessage? limitedResponse = null;

        for (int i = 0; i <= AuthPolicyPermitLimit; i++)
        {
            limitedResponse = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);
        }

        limitedResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task AuthEndpoint_WhenRateLimitExceeded_ResponseBodyIsProblemDetails()
    {
        var client = fixture.CreateAnonymousClient();

        HttpResponseMessage? limitedResponse = null;

        for (int i = 0; i <= AuthPolicyPermitLimit; i++)
        {
            limitedResponse = await client.PostAsJsonAsync(AuthLoginPath, InvalidLoginPayload);
        }

        limitedResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResponse.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
    }
}
