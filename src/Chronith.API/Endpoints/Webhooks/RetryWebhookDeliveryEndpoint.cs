using Chronith.Application.Commands.Webhooks.RetryWebhookDelivery;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class RetryWebhookDeliveryEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/webhooks/{webhookId}/deliveries/{deliveryId}/retry");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.WebhooksWrite}");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
        Summary(s =>
        {
            s.Summary = "Manually retry a failed webhook delivery";
            s.Description = "Resets a Failed delivery attempt back to Pending for immediate retry. Only valid for Failed entries.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var webhookId = Route<Guid>("webhookId");
        var deliveryId = Route<Guid>("deliveryId");
        await sender.Send(new RetryWebhookDeliveryCommand(webhookId, deliveryId), ct);
        await Send.NoContentAsync(ct);
    }
}
