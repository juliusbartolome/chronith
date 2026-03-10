using System.Net;
using System.Net.Http.Json;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.HealthChecks;

[Collection("Functional")]
public sealed class HealthCheckTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task GetHealthLive_ReturnsHealthy()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealthReady_IncludesDatabaseCheck()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready?format=json");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("database");
    }

    [Fact]
    public async Task GetHealthReady_IncludesBackgroundServicesCheck()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready?format=json");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("background-services");
    }

    [Fact]
    public async Task GetHealthReady_CorrelationIdHeaderIsPresent()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }
}
