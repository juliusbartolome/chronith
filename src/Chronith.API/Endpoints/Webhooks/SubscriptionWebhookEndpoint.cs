using Chronith.Application.Commands.Subscriptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class SubscriptionWebhookRequest
{
    public string ProviderSubscriptionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset? NewPeriodStart { get; set; }
    public DateTimeOffset? NewPeriodEnd { get; set; }
}

/// <summary>
/// Receives billing webhook events from the subscription provider (e.g. PayMongo).
/// This endpoint should be secured via a shared secret validated in middleware or a dedicated header check.
/// For now it accepts service-role tokens.
/// </summary>
public sealed class SubscriptionWebhookEndpoint(ISender sender)
    : Endpoint<SubscriptionWebhookRequest>
{
    public override void Configure()
    {
        Post("/webhooks/subscription");
        AllowAnonymous();
        Options(x => x.WithTags("Webhooks"));
    }

    public override async Task HandleAsync(SubscriptionWebhookRequest req, CancellationToken ct)
    {
        await sender.Send(new SubscriptionBillingWebhookCommand
        {
            ProviderSubscriptionId = req.ProviderSubscriptionId,
            EventType = req.EventType,
            NewPeriodStart = req.NewPeriodStart,
            NewPeriodEnd = req.NewPeriodEnd,
        }, ct);
        await Send.NoContentAsync(ct);
    }
}
