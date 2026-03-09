using System.Net;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;

namespace Chronith.Tests.Functional.Versioning;

[Collection("Functional")]
public sealed class VersionRedirectTests(FunctionalTestFixture fixture)
{
    private HttpClient CreateNonRedirectingClient()
    {
        var handler = fixture.Factory.Server.CreateHandler();
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    [Theory]
    [InlineData("/booking-types")]
    [InlineData("/bookings")]
    [InlineData("/auth/login")]
    [InlineData("/staff")]
    [InlineData("/tenant")]
    [InlineData("/webhooks")]
    [InlineData("/analytics/summary")]
    [InlineData("/waitlist")]
    [InlineData("/time-blocks")]
    [InlineData("/api-keys")]
    [InlineData("/notifications")]
    [InlineData("/payments")]
    [InlineData("/integrations/ical")]
    [InlineData("/public/booking-types")]
    public async Task UnversionedRoute_Returns308_WithV1LocationHeader(string path)
    {
        var client = CreateNonRedirectingClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be((HttpStatusCode)308);
        response.Headers.Location!.ToString().Should().Be($"/v1{path}");
    }

    [Fact]
    public async Task UnversionedRoute_WithQueryString_PreservesQueryStringInLocation()
    {
        var client = CreateNonRedirectingClient();

        var response = await client.GetAsync("/booking-types?page=1&size=10");

        response.StatusCode.Should().Be((HttpStatusCode)308);
        response.Headers.Location!.ToString().Should().Be("/v1/booking-types?page=1&size=10");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_ReturnsOk_NotRedirect(string path)
    {
        var client = CreateNonRedirectingClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task V1Route_IsNotRedirected()
    {
        var client = CreateNonRedirectingClient();

        // /v1/health-check won't exist, but we should NOT get a 301 redirect
        var response = await client.GetAsync("/v1/booking-types");

        response.StatusCode.Should().NotBe((HttpStatusCode)308);
    }

    [Fact]
    public async Task UnversionedRoute_Post_Returns308_WithV1LocationHeader()
    {
        var client = CreateNonRedirectingClient();

        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/booking-types/test-type/bookings", content);

        response.StatusCode.Should().Be((HttpStatusCode)308);
        response.Headers.Location!.ToString().Should().Be("/v1/booking-types/test-type/bookings");
    }
}
