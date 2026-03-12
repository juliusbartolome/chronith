using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

namespace Chronith.Domain.Models;

public sealed class TenantSubscription
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset? TrialEndsAt { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public string? PaymentProviderSubscriptionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }

    public bool IsExpiredOrCancelled =>
        Status is SubscriptionStatus.Expired or SubscriptionStatus.Cancelled;

    public static TenantSubscription CreateTrial(Guid tenantId, Guid planId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = now.AddDays(14),
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(14),
            CreatedAt = now,
        };
    }

    public static TenantSubscription CreatePaid(
        Guid tenantId,
        Guid planId,
        string paymentProviderSubscriptionId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        return new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Active,
            PaymentProviderSubscriptionId = paymentProviderSubscriptionId,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    internal TenantSubscription() { } // EF Core hydration only

    public void SetPastDue()
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing))
            throw new InvalidStateTransitionException(
                $"Cannot transition '{nameof(TenantSubscription)}' from '{Status}' to '{nameof(SubscriptionStatus.PastDue)}'.");
        Status = SubscriptionStatus.PastDue;
    }

    public void Cancel(string? reason)
    {
        if (Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
            throw new InvalidStateTransitionException(
                $"Cannot transition '{nameof(TenantSubscription)}' from '{Status}' to '{nameof(SubscriptionStatus.Cancelled)}'.");
        Status = SubscriptionStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        CancelReason = reason;
    }

    public void Expire()
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue))
            throw new InvalidStateTransitionException(
                $"Cannot transition '{nameof(TenantSubscription)}' from '{Status}' to '{nameof(SubscriptionStatus.Expired)}'.");
        Status = SubscriptionStatus.Expired;
    }

    public void RenewPeriod(DateTimeOffset newPeriodStart, DateTimeOffset newPeriodEnd)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
            throw new InvalidStateTransitionException(
                $"Cannot transition '{nameof(TenantSubscription)}' from '{Status}' to '{nameof(SubscriptionStatus.Active)}' via renewal.");
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = newPeriodStart;
        CurrentPeriodEnd = newPeriodEnd;
    }

    public void Activate(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (Status is not (SubscriptionStatus.Trialing or SubscriptionStatus.PastDue))
            throw new InvalidStateTransitionException(
                $"Cannot transition '{nameof(TenantSubscription)}' from '{Status}' to '{nameof(SubscriptionStatus.Active)}' via activation.");
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
    }
}
