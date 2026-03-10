using System.Net;
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

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("database");
    }

    [Fact]
    public async Task GetHealthReady_IncludesBackgroundServicesCheck()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("background-services");
    }

    // Redis:Enabled is false in the functional test environment (FunctionalTestFixture starts only a
    // PostgreSQL Testcontainer — no Redis container). RedisHealthCheck is conditionally registered
    // only when Redis:Enabled = true, so "redis" will never appear in /health/ready responses here.
    // This test is skipped to document the gap; run against a stack with Redis:Enabled=true to exercise it.
    [Fact(Skip = "Redis not in functional test stack — Redis:Enabled=false, RedisHealthCheck not registered")]
    public async Task GetHealthReady_IncludesRedisCheck()
    {
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("redis");
    }

    [Fact]
    public async Task GetHealthReady_CorrelationIdHeaderIsPresent()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");

        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }
}
