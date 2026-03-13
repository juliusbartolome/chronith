using System.Net;
using Chronith.Application.DTOs;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Plans;

[Collection("Functional")]
public sealed class PlansEndpointsTests(FunctionalTestFixture fixture)
{
    [Fact]
    public async Task GetPlans_Anonymous_Returns200WithPlans()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<IReadOnlyList<TenantPlanDto>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
        body!.Should().HaveCountGreaterThanOrEqualTo(4);
        body.Should().Contain(p => p.Name == "Free");
        body.Should().Contain(p => p.Name == "Starter");
        body.Should().Contain(p => p.Name == "Pro");
        body.Should().Contain(p => p.Name == "Enterprise");
    }

    [Fact]
    public async Task GetPlans_ReturnsAtLeastFreePlan()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<IReadOnlyList<TenantPlanDto>>();
        body!.Should().Contain(p => p.Name == "Free");
    }

    [Fact]
    public async Task GetPlans_AllPlansHaveValidShape()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync("/v1/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadFromApiJsonAsync<IReadOnlyList<TenantPlanDto>>();
        body!.Should().AllSatisfy(p =>
        {
            p.Id.Should().NotBeEmpty();
            p.Name.Should().NotBeNullOrEmpty();
            p.MaxBookingTypes.Should().BeGreaterThanOrEqualTo(0);
            p.PriceCentavos.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task GetPlans_AsAdmin_Returns200()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/plans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
