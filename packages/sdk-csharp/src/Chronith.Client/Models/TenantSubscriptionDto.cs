namespace Chronith.Client.Models;

public sealed record TenantSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string PlanName,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    string? PaymentProviderSubscriptionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason
);
