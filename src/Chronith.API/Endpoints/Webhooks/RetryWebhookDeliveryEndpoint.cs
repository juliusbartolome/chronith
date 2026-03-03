using Chronith.Application.Commands.Webhooks.RetryWebhookDelivery;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class RetryWebhookDeliveryRequest
{
    public Guid WebhookId { get; set; }
    public Guid DeliveryId { get; set; }
}

public sealed class RetryWebhookDeliveryEndpoint(ISender sender)
    : Endpoint<RetryWebhookDeliveryRequest>
{
    public override void Configure()
    {
        Post("/webhooks/{webhookId}/deliveries/{deliveryId}/retry");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Webhooks"));
        Summary(s =>
        {
            s.Summary = "Manually retry a failed webhook delivery";
            s.Description = "Resets a Failed delivery attempt back to Pending for immediate retry. Only valid for Failed entries.";
        });
    }

    public override async Task HandleAsync(RetryWebhookDeliveryRequest req, CancellationToken ct)
    {
        await sender.Send(new RetryWebhookDeliveryCommand(req.WebhookId, req.DeliveryId), ct);
        await Send.NoContentAsync(ct);
    }
}
