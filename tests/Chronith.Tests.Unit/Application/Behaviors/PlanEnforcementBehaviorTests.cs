using Chronith.Application.Behaviors;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Chronith.Tests.Unit.Application.Behaviors;

public sealed class PlanEnforcementBehaviorTests
{
    // ── Test request types ────────────────────────────────────────────────────

    private sealed record NonEnforcedRequest : IRequest<string>;

    private sealed record EnforcedRequest(string ResourceType)
        : IRequest<string>, IPlanEnforcedCommand
    {
        string IPlanEnforcedCommand.EnforcedResourceType => ResourceType;
    }

    // ── Shared mocks ──────────────────────────────────────────────────────────

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

    private PlanEnforcementBehavior<TRequest, string> CreateBehavior<TRequest>()
        where TRequest : notnull =>
        new(
            _subRepo, _planRepo, _btRepo, _staffRepo, _bookingRepo, _customerRepo,
            _tenantContext,
            NullLogger<PlanEnforcementBehavior<TRequest, string>>.Instance);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCommandDoesNotImplementIPlanEnforcedCommand_PassesThrough()
    {
        // Arrange
        var behavior = CreateBehavior<NonEnforcedRequest>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(new NonEnforcedRequest(), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
        await _subRepo.DidNotReceive().GetActiveByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoActiveSubscription_PassesThrough()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns((TenantSubscription?)null);

        var behavior = CreateBehavior<EnforcedRequest>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(
            new EnforcedRequest("BookingType"), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNoPlanFound_PassesThrough()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var sub = TenantSubscription.CreateTrial(tenantId, Guid.NewGuid());
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        _planRepo.GetByIdAsync(sub.PlanId, Arg.Any<CancellationToken>())
            .Returns((TenantPlan?)null);

        var behavior = CreateBehavior<EnforcedRequest>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(
            new EnforcedRequest("BookingType"), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAtBookingTypeLimit_ThrowsPlanLimitExceededException()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var plan = TenantPlan.Create("Starter", 5, 3, 500, 500, true, false, false, false, false, 100000, 1);
        var sub = TenantSubscription.CreateTrial(tenantId, plan.Id);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        _planRepo.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>())
            .Returns(plan);

        // At the limit: count == MaxBookingTypes
        _btRepo.CountByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(plan.MaxBookingTypes);

        var behavior = CreateBehavior<EnforcedRequest>();
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        var act = async () => await behavior.Handle(
            new EnforcedRequest("BookingType"), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PlanLimitExceededException>()
            .Where(ex => ex.ResourceType == "BookingType");
    }

    [Fact]
    public async Task Handle_WhenBelowBookingTypeLimit_PassesThrough()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var plan = TenantPlan.Create("Starter", 5, 3, 500, 500, true, false, false, false, false, 100000, 1);
        var sub = TenantSubscription.CreateTrial(tenantId, plan.Id);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        _planRepo.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>())
            .Returns(plan);

        // Below the limit: count == MaxBookingTypes - 1
        _btRepo.CountByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(plan.MaxBookingTypes - 1);

        var behavior = CreateBehavior<EnforcedRequest>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(
            new EnforcedRequest("BookingType"), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenUnknownResourceType_PassesThrough()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var plan = TenantPlan.Create("Starter", 5, 3, 500, 500, true, false, false, false, false, 100000, 1);
        var sub = TenantSubscription.CreateTrial(tenantId, plan.Id);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(sub);
        _planRepo.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>())
            .Returns(plan);

        var behavior = CreateBehavior<EnforcedRequest>();
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(
            new EnforcedRequest("UnknownResource"), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
    }
}
