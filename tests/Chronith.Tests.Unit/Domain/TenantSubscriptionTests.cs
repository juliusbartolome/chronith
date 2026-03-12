using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Chronith.Tests.Unit.Domain;

public class TenantSubscriptionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public void CreateTrial_SetsStatusTrialingAndTrialEndsAt14Days()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);

        sub.Id.Should().NotBeEmpty();
        sub.TenantId.Should().Be(TenantId);
        sub.PlanId.Should().Be(PlanId);
        sub.Status.Should().Be(SubscriptionStatus.Trialing);
        sub.TrialEndsAt.Should().NotBeNull();
        sub.TrialEndsAt!.Value.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromSeconds(5));
        sub.CurrentPeriodStart.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        sub.CurrentPeriodEnd.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddDays(14), TimeSpan.FromSeconds(5));
        sub.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreatePaid_SetsStatusActive()
    {
        var periodStart = DateTimeOffset.UtcNow;
        var periodEnd = periodStart.AddDays(30);

        var sub = TenantSubscription.CreatePaid(
            TenantId, PlanId, "pay_sub_123", periodStart, periodEnd);

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.PaymentProviderSubscriptionId.Should().Be("pay_sub_123");
        sub.CurrentPeriodStart.Should().Be(periodStart);
        sub.CurrentPeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public void SetPastDue_SetsStatusPastDue()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);

        sub.SetPastDue();

        sub.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public void Cancel_SetsStatusCancelledAndCancelledAt()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);

        sub.Cancel("No longer needed");

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAt.Should().NotBeNull();
        sub.CancelledAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        sub.CancelReason.Should().Be("No longer needed");
    }

    [Fact]
    public void Expire_SetsStatusExpired()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);

        sub.Expire();

        sub.Status.Should().Be(SubscriptionStatus.Expired);
    }

    [Fact]
    public void RenewPeriod_SetsStatusActiveAndUpdatesPeriod()
    {
        var periodStart = DateTimeOffset.UtcNow;
        var periodEnd = periodStart.AddDays(30);
        var sub = TenantSubscription.CreatePaid(TenantId, PlanId, "pay_sub_456", periodStart, periodEnd);
        var newStart = periodEnd;
        var newEnd = newStart.AddDays(30);

        sub.RenewPeriod(newStart, newEnd);

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CurrentPeriodStart.Should().Be(newStart);
        sub.CurrentPeriodEnd.Should().Be(newEnd);
    }

    [Fact]
    public void IsExpiredOrCancelled_TrueWhenExpired()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);
        sub.Expire();

        sub.IsExpiredOrCancelled.Should().BeTrue();
    }

    [Fact]
    public void IsExpiredOrCancelled_TrueWhenCancelled()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);
        sub.Cancel(null);

        sub.IsExpiredOrCancelled.Should().BeTrue();
    }

    [Fact]
    public void IsExpiredOrCancelled_FalseWhenActive()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);

        sub.IsExpiredOrCancelled.Should().BeFalse();
    }

    [Fact]
    public void SetPastDue_WhenCancelled_ThrowsInvalidStateTransitionException()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);
        sub.Cancel(null);

        sub.Invoking(s => s.SetPastDue())
            .Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsInvalidStateTransitionException()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);
        sub.Cancel(null);

        sub.Invoking(s => s.Cancel(null))
            .Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void RenewPeriod_WhenCancelled_ThrowsInvalidStateTransitionException()
    {
        var sub = TenantSubscription.CreateTrial(TenantId, PlanId);
        sub.Cancel(null);

        sub.Invoking(s => s.RenewPeriod(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30)))
            .Should().Throw<InvalidStateTransitionException>();
    }
}
