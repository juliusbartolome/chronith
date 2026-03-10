using System.Net;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Security;

[Collection("Functional")]
public sealed class CorsTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task Request_WithOriginHeader_ExposesCorrelationIdHeader()
    {
        // When AllowedOrigins is empty, AllowAnyOrigin() is used — any origin is accepted.
        // Verify that Access-Control-Expose-Headers contains X-Correlation-Id.
        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Origin", "https://example.com");

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Expose-Headers").Should().BeTrue();
        var exposeHeader = response.Headers.GetValues("Access-Control-Expose-Headers").First();
        exposeHeader.Should().Contain("X-Correlation-Id");
    }

    [Fact]
    public async Task Request_WithOriginHeader_ReturnsAccessControlAllowOriginHeader()
    {
        // When AllowedOrigins is empty, AllowAnyOrigin() is used — wildcard or echo is returned.
        var client = fixture.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Origin", "https://example.com");

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
    }

    [Fact]
    public async Task PreflightRequest_ReturnsOkWithCorsHeaders()
    {
        // OPTIONS preflight should return 204 (No Content) with CORS headers
        // when AllowedOrigins is empty (AllowAnyOrigin).
        var client = fixture.CreateAnonymousClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/health/ready");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.Contains("Access-Control-Allow-Headers").Should().BeTrue();
    }
}
