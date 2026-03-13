using Chronith.Application.Commands.Subscriptions;
using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Configuration;

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
/// Authenticated via X-Webhook-Secret header when Webhooks:SigningSecret is configured.
/// </summary>
public sealed class SubscriptionWebhookEndpoint(ISender sender, IConfiguration configuration)
    : Endpoint<SubscriptionWebhookRequest>
{
    public override void Configure()
    {
        Post("/webhooks/subscription");
        AllowAnonymous(); // Public — validated via shared secret header
        Options(x => x.WithTags("Webhooks"));
    }

    public override async Task HandleAsync(SubscriptionWebhookRequest req, CancellationToken ct)
    {
        var secret = configuration["Webhooks:SigningSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var incoming = HttpContext.Request.Headers["X-Webhook-Secret"].FirstOrDefault();
            if (incoming != secret)
            {
                await Send.UnauthorizedAsync(ct);
                return;
            }
        }

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
