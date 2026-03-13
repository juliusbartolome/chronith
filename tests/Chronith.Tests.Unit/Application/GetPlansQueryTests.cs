using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Plans;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chronith.Tests.Unit.Application;

public sealed class GetPlansQueryTests
{
    private readonly ITenantPlanRepository _repo = Substitute.For<ITenantPlanRepository>();

    [Fact]
    public async Task Handle_ReturnsActivePlansAsDtos()
    {
        var plan = TenantPlan.Create("Free", 1, 0, 50, 50, false, false, false, false, false, 0, 0);
        _repo.GetActivePlansAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TenantPlan> { plan }.AsReadOnly());

        var handler = new GetPlansQueryHandler(_repo);
        var result = await handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Free");
    }

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsEmptyList()
    {
        _repo.GetActivePlansAsync(Arg.Any<CancellationToken>())
            .Returns(new List<TenantPlan>().AsReadOnly());

        var handler = new GetPlansQueryHandler(_repo);
        var result = await handler.Handle(new GetPlansQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
