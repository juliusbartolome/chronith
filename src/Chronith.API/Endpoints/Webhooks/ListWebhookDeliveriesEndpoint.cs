using Chronith.Application.DTOs;
using Chronith.Application.Queries.Webhooks.ListWebhookDeliveries;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Webhooks;

public sealed class ListWebhookDeliveriesRequest
{
    public Guid WebhookId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListWebhookDeliveriesEndpoint(ISender sender)
    : Endpoint<ListWebhookDeliveriesRequest, PagedResultDto<WebhookDeliveryDto>>
{
    public override void Configure()
    {
        Get("/webhooks/{webhookId}/deliveries");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.WebhooksRead}");
        Options(x => x.WithTags("Webhooks").RequireRateLimiting("Authenticated"));
        Summary(s =>
        {
            s.Summary = "List webhook delivery attempts";
            s.Description = "Returns a paginated list of delivery attempts for a webhook subscription.";
        });
    }

    public override async Task HandleAsync(ListWebhookDeliveriesRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new ListWebhookDeliveriesQuery(req.WebhookId, req.Page, req.PageSize), ct);
        await Send.OkAsync(result, ct);
    }
}
