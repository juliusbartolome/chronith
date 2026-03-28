using Chronith.Application.Commands.Webhooks;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class UpdateWebhookRequest
{
    // Route params
    public string Slug { get; set; } = string.Empty;
    public Guid WebhookId { get; set; }

    // Body (all optional for partial update)
    public string? Url { get; set; }
    public string? Secret { get; set; }
    public List<string>? EventTypes { get; set; }
}

public sealed class UpdateWebhookEndpoint(ISender sender)
    : Endpoint<UpdateWebhookRequest, WebhookDto>
{
    public override void Configure()
    {
        Patch("/booking-types/{slug}/webhooks/{webhookId}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.WebhooksWrite}");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateWebhookRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateWebhookCommand
        {
            BookingTypeSlug = req.Slug,
            WebhookId = req.WebhookId,
            Url = req.Url,
            Secret = req.Secret,
            EventTypes = req.EventTypes
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
