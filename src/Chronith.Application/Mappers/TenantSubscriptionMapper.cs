using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(this TenantSubscription sub, string planName) => new(
        sub.Id,
        sub.TenantId,
        sub.PlanId,
        planName,
        sub.Status.ToString(),
        sub.TrialEndsAt,
        sub.CurrentPeriodStart,
        sub.CurrentPeriodEnd,
        sub.PaymentProviderSubscriptionId,
        sub.CreatedAt,
        sub.CancelledAt,
        sub.CancelReason
    );
}
