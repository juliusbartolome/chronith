namespace Chronith.Client.Models;

public sealed record TenantSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string PlanName,
    string PlanTier,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? CurrentPeriodStart,
    DateTimeOffset? CurrentPeriodEnd
);
