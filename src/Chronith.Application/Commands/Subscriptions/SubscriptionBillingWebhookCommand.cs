using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Commands.Subscriptions;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Handles incoming billing webhook events from the subscription provider.
/// Event types: "subscription.renewed", "subscription.past_due", "subscription.expired"
/// </summary>
public sealed record SubscriptionBillingWebhookCommand : IRequest<Unit>
{
    public required string ProviderSubscriptionId { get; init; }
    public required string EventType { get; init; }
    public DateTimeOffset? NewPeriodStart { get; init; }
    public DateTimeOffset? NewPeriodEnd { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class SubscriptionBillingWebhookCommandHandler(
    ITenantSubscriptionRepository subRepo,
    IUnitOfWork unitOfWork,
    ILogger<SubscriptionBillingWebhookCommandHandler> logger
) : IRequestHandler<SubscriptionBillingWebhookCommand, Unit>
{
    public async Task<Unit> Handle(
        SubscriptionBillingWebhookCommand command, CancellationToken cancellationToken)
    {
        var sub = await subRepo.GetByProviderIdAsync(
            command.ProviderSubscriptionId, cancellationToken);

        if (sub is null)
        {
            logger.LogWarning(
                "Billing webhook: subscription {ProviderId} not found, skipping event {EventType}",
                command.ProviderSubscriptionId, command.EventType);
            return Unit.Value;
        }

        switch (command.EventType)
        {
            case "subscription.renewed":
                if (command.NewPeriodStart.HasValue && command.NewPeriodEnd.HasValue)
                    sub.RenewPeriod(command.NewPeriodStart.Value, command.NewPeriodEnd.Value);
                break;

            case "subscription.past_due":
                sub.SetPastDue();
                break;

            case "subscription.expired":
                sub.Expire();
                break;

            default:
                logger.LogInformation(
                    "Billing webhook: unhandled event type {EventType} for subscription {ProviderId}",
                    command.EventType, command.ProviderSubscriptionId);
                return Unit.Value;
        }

        await subRepo.UpdateAsync(sub, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
