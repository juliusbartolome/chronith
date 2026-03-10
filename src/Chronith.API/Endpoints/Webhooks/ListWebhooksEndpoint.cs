using Chronith.Application.DTOs;
using Chronith.Application.Queries.Webhooks;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class ListWebhooksRequest
{
    public string Slug { get; set; } = string.Empty;
}

public sealed class ListWebhooksEndpoint(ISender sender)
    : Endpoint<ListWebhooksRequest, IReadOnlyList<WebhookDto>>
{
    public override void Configure()
    {
        Get("/booking-types/{slug}/webhooks");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(ListWebhooksRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetWebhooksQuery(req.Slug), ct);
        await Send.OkAsync(result, ct);
    }
}
