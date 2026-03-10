using System.Net;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Security;

[Collection("Functional")]
public sealed class SecurityHeadersTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task GetHealthReady_SetsContentSecurityPolicyHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        csp.Should().Contain("default-src");
        csp.Should().Contain("frame-ancestors");
    }

    [Fact]
    public async Task GetHealthReady_SetsXContentTypeOptionsHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").First()
            .Should().Be("nosniff");
    }

    [Fact]
    public async Task GetHealthReady_SetsXFrameOptionsHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("X-Frame-Options").Should().BeTrue();
        response.Headers.GetValues("X-Frame-Options").First()
            .Should().Be("DENY");
    }

    [Fact]
    public async Task GetHealthReady_SetsStrictTransportSecurityHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("Strict-Transport-Security").Should().BeTrue();
        response.Headers.GetValues("Strict-Transport-Security").First()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task GetHealthReady_SetsReferrerPolicyHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("Referrer-Policy").Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").First()
            .Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task GetHealthReady_SetsPermissionsPolicyHeader()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("Permissions-Policy").Should().BeTrue();
        response.Headers.GetValues("Permissions-Policy").First()
            .Should().Be("camera=(), microphone=(), geolocation=()");
    }
}
