using Chronith.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="ISubscriptionProvider"/> used in development and testing.
/// All operations return successful results without contacting any external billing provider.
/// </summary>
public sealed class StubSubscriptionProvider(
    ILogger<StubSubscriptionProvider> logger
) : ISubscriptionProvider
{
    public Task<SubscriptionResult> CreateSubscriptionAsync(
        CreateSubscriptionRequest request, CancellationToken ct)
    {
        var id = $"stub-sub-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        logger.LogInformation(
            "StubSubscriptionProvider: created subscription {SubscriptionId} for tenant {TenantId}, plan {PlanId}",
            id, request.TenantId, request.PlanId);

        return Task.FromResult(new SubscriptionResult(id, now, now.AddMonths(1)));
    }

    public Task<SubscriptionResult> UpdateSubscriptionAsync(
        string subscriptionId, UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        logger.LogInformation(
            "StubSubscriptionProvider: updated subscription {SubscriptionId} to plan {PlanId}",
            subscriptionId, request.NewPlanId);

        return Task.FromResult(new SubscriptionResult(subscriptionId, now, now.AddMonths(1)));
    }

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        logger.LogInformation(
            "StubSubscriptionProvider: cancelled subscription {SubscriptionId}", subscriptionId);

        return Task.CompletedTask;
    }

    public Task<SubscriptionStatusResult> GetSubscriptionStatusAsync(
        string subscriptionId, CancellationToken ct)
    {
        return Task.FromResult(
            new SubscriptionStatusResult("active", DateTimeOffset.UtcNow.AddMonths(1)));
    }
}
