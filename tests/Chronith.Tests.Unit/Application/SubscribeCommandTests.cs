using Chronith.Application.Commands.Subscriptions;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chronith.Tests.Unit.Application;

public sealed class SubscribeCommandTests
{
    private readonly ITenantSubscriptionRepository _subRepo =
        Substitute.For<ITenantSubscriptionRepository>();
    private readonly ITenantPlanRepository _planRepo =
        Substitute.For<ITenantPlanRepository>();
    private readonly ISubscriptionProvider _provider =
        Substitute.For<ISubscriptionProvider>();
    private readonly ITenantContext _tenantContext =
        Substitute.For<ITenantContext>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_FreePlan_CreatesTrial()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var freePlan = TenantPlan.Create("Free", 1, 0, 50, 50, false, false, false, false, false, 0, 0);
        _planRepo.GetByIdAsync(freePlan.Id, Arg.Any<CancellationToken>())
            .Returns(freePlan);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(default(TenantSubscription));

        var handler = new SubscribeCommandHandler(
            _subRepo, _planRepo, _provider, _tenantContext, _uow);

        var result = await handler.Handle(
            new SubscribeCommand { PlanId = freePlan.Id },
            CancellationToken.None);

        result.Status.Should().Be("Trialing");
        await _subRepo.Received(1).AddAsync(
            Arg.Any<TenantSubscription>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingActiveSubscription_ThrowsConflict()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var freePlan = TenantPlan.Create("Free", 1, 0, 50, 50, false, false, false, false, false, 0, 0);
        _planRepo.GetByIdAsync(freePlan.Id, Arg.Any<CancellationToken>())
            .Returns(freePlan);

        var existingSub = TenantSubscription.CreateTrial(tenantId, freePlan.Id);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(existingSub);

        var handler = new SubscribeCommandHandler(
            _subRepo, _planRepo, _provider, _tenantContext, _uow);

        var act = async () => await handler.Handle(
            new SubscribeCommand { PlanId = freePlan.Id },
            CancellationToken.None);

        await act.Should().ThrowAsync<Chronith.Domain.Exceptions.ConflictException>();
    }

    [Fact]
    public async Task Handle_PaidPlan_CallsProviderAndCreatesPaidSubscription()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var paidPlan = TenantPlan.Create("Starter", 5, 3, 500, 500, true, false, false, false, false, 100000, 1);
        _planRepo.GetByIdAsync(paidPlan.Id, Arg.Any<CancellationToken>())
            .Returns(paidPlan);
        _subRepo.GetActiveByTenantIdAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(default(TenantSubscription));

        var now = DateTimeOffset.UtcNow;
        _provider.CreateSubscriptionAsync(
                Arg.Any<CreateSubscriptionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionResult("prov-123", now, now.AddMonths(1)));

        var handler = new SubscribeCommandHandler(
            _subRepo, _planRepo, _provider, _tenantContext, _uow);

        var result = await handler.Handle(
            new SubscribeCommand { PlanId = paidPlan.Id, PaymentMethodToken = "tok_test" },
            CancellationToken.None);

        result.Status.Should().Be("Active");
        await _provider.Received(1).CreateSubscriptionAsync(
            Arg.Any<CreateSubscriptionRequest>(), Arg.Any<CancellationToken>());
        await _subRepo.Received(1).AddAsync(
            Arg.Any<TenantSubscription>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
