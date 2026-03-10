using Chronith.Application.Commands.Webhooks;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class DeleteWebhookRequest
{
    public string Slug { get; set; } = string.Empty;
    public Guid WebhookId { get; set; }
}

public sealed class DeleteWebhookEndpoint(ISender sender)
    : Endpoint<DeleteWebhookRequest>
{
    public override void Configure()
    {
        Delete("/booking-types/{slug}/webhooks/{webhookId}");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(DeleteWebhookRequest req, CancellationToken ct)
    {
        await sender.Send(new DeleteWebhookCommand
        {
            BookingTypeSlug = req.Slug,
            WebhookId = req.WebhookId
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
