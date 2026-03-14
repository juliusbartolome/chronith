namespace Chronith.Application.Interfaces;

public interface ISubscriptionProvider
{
    Task<SubscriptionResult> CreateSubscriptionAsync(
        CreateSubscriptionRequest request, CancellationToken ct);

    Task<SubscriptionResult> UpdateSubscriptionAsync(
        string subscriptionId, UpdateSubscriptionRequest request, CancellationToken ct);

    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);

    Task<SubscriptionStatusResult> GetSubscriptionStatusAsync(
        string subscriptionId, CancellationToken ct);
}

public sealed record CreateSubscriptionRequest(
    Guid TenantId,
    Guid PlanId,
    string CustomerEmail,
    string? PaymentMethodToken);

public sealed record UpdateSubscriptionRequest(Guid NewPlanId);

public sealed record SubscriptionResult(
    string ProviderSubscriptionId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

public sealed record SubscriptionStatusResult(string Status, DateTimeOffset? PeriodEnd);
