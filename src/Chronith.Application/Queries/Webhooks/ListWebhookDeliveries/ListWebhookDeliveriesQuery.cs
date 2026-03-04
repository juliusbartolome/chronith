using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Webhooks.ListWebhookDeliveries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListWebhookDeliveriesQuery(
    Guid WebhookId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResultDto<WebhookDeliveryDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListWebhookDeliveriesQueryHandler(
    ITenantContext tenantContext,
    IWebhookRepository webhookRepository,
    IWebhookOutboxRepository outboxRepository)
    : IRequestHandler<ListWebhookDeliveriesQuery, PagedResultDto<WebhookDeliveryDto>>
{
    public async Task<PagedResultDto<WebhookDeliveryDto>> Handle(
        ListWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        // Verify the webhook exists and belongs to this tenant
        _ = await webhookRepository.GetByIdAsync(tenantContext.TenantId, request.WebhookId, cancellationToken)
            ?? throw new NotFoundException("Webhook", request.WebhookId);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var (items, total) = await outboxRepository.ListByWebhookAsync(
            request.WebhookId, request.Page, pageSize, cancellationToken);

        return new PagedResultDto<WebhookDeliveryDto>(items, total, request.Page, pageSize);
    }
}
