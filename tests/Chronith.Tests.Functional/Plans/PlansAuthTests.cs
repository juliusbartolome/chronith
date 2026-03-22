using System.Net;
using Chronith.Tests.Functional.Fixtures;

namespace Chronith.Tests.Functional.Plans;

[Collection("Functional")]
public sealed class PlansAuthTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task GetPlans_Anonymous_Returns200()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
