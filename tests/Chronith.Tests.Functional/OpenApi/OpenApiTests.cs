using System.Net;
using System.Text.Json;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;
using Xunit;

namespace Chronith.Tests.Functional.OpenApi;

[Collection("Functional")]
public sealed class OpenApiTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task OpenApiJson_InNonProduction_Returns200WithValidJson()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/openapi.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Contain("json");

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content); // throws if invalid JSON
        doc.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
        doc.RootElement.GetProperty("info").GetProperty("title").GetString()
            .Should().Be("Chronith API");
    }

    [Fact]
    public async Task SwaggerUi_InNonProduction_Returns200WithHtml()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/swagger");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("swagger");
    }

    [Fact]
    public async Task OpenApiJson_DocumentsAllExpectedTags()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/openapi.json");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        var tags = doc.RootElement
            .GetProperty("tags")
            .EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToList();

        tags.Should().Contain(["Bookings", "BookingTypes", "Availability", "Webhooks", "Tenant", "Payments"]);
    }
}
