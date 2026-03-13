using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Subscriptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chronith.Tests.Unit.Application;

public sealed class GetUsageQueryTests
{
    private readonly ITenantSubscriptionRepository _subRepo =
        Substitute.For<ITenantSubscriptionRepository>();
    private readonly ITenantPlanRepository _planRepo =
        Substitute.For<ITenantPlanRepository>();
    private readonly IBookingTypeRepository _btRepo =
        Substitute.For<IBookingTypeRepository>();
    private readonly IStaffMemberRepository _staffRepo =
        Substitute.For<IStaffMemberRepository>();
    private readonly IBookingRepository _bookingRepo =
        Substitute.For<IBookingRepository>();
    private readonly ICustomerRepository _customerRepo =
        Substitute.For<ICustomerRepository>();
    private readonly ITenantContext _tenantContext =
        Substitute.For<ITenantContext>();

    [Fact]
    public async Task Handle_ReturnsCorrectUsageCounts()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var plan = TenantPlan.Create("Free", 1, 0, 50, 50, false, false, false, false, false, 0, 0);
        var sub = TenantSubscription.CreateTrial(tenantId, plan.Id);

        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        _planRepo.GetByIdAsync(sub.PlanId, Arg.Any<CancellationToken>())
            .Returns(plan);

        _btRepo.CountByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(0);
        _staffRepo.CountByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(0);
        _bookingRepo.CountByTenantSinceAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _customerRepo.CountByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(0);

        var handler = new GetUsageQueryHandler(
            _subRepo, _planRepo, _btRepo, _staffRepo, _bookingRepo, _customerRepo, _tenantContext);

        var result = await handler.Handle(new GetUsageQuery(), CancellationToken.None);

        result.BookingTypesLimit.Should().Be(1);
        result.StaffMembersLimit.Should().Be(0);
        result.BookingsPerMonthLimit.Should().Be(50);
        result.CustomersLimit.Should().Be(50);
        result.PlanName.Should().Be("Free");
        result.BookingTypesUsed.Should().Be(0);
    }
}
