using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantSubscriptionEntityMapper
{
    public static TenantSubscription ToDomain(TenantSubscriptionEntity entity)
    {
        var domain = new TenantSubscription();
        SetProperty(domain, nameof(TenantSubscription.Id), entity.Id);
        SetProperty(domain, nameof(TenantSubscription.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TenantSubscription.PlanId), entity.PlanId);
        SetProperty(domain, nameof(TenantSubscription.Status), Enum.Parse<SubscriptionStatus>(entity.Status));
        SetProperty(domain, nameof(TenantSubscription.TrialEndsAt), entity.TrialEndsAt);
        SetProperty(domain, nameof(TenantSubscription.CurrentPeriodStart), entity.CurrentPeriodStart);
        SetProperty(domain, nameof(TenantSubscription.CurrentPeriodEnd), entity.CurrentPeriodEnd);
        SetProperty(domain, nameof(TenantSubscription.PaymentProviderSubscriptionId), entity.PaymentProviderSubscriptionId);
        SetProperty(domain, nameof(TenantSubscription.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(TenantSubscription.CancelledAt), entity.CancelledAt);
        SetProperty(domain, nameof(TenantSubscription.CancelReason), entity.CancelReason);
        return domain;
    }

    public static TenantSubscriptionEntity ToEntity(TenantSubscription domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            PlanId = domain.PlanId,
            Status = domain.Status.ToString(),
            TrialEndsAt = domain.TrialEndsAt,
            CurrentPeriodStart = domain.CurrentPeriodStart,
            CurrentPeriodEnd = domain.CurrentPeriodEnd,
            PaymentProviderSubscriptionId = domain.PaymentProviderSubscriptionId,
            CreatedAt = domain.CreatedAt,
            CancelledAt = domain.CancelledAt,
            CancelReason = domain.CancelReason,
            IsDeleted = false,
        };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        prop?.SetValue(target, value);
    }
}
