using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Webhooks;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetWebhooksQuery(string BookingTypeSlug) : IRequest<IReadOnlyList<WebhookDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetWebhooksHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IWebhookRepository webhookRepo)
    : IRequestHandler<GetWebhooksQuery, IReadOnlyList<WebhookDto>>
{
    public async Task<IReadOnlyList<WebhookDto>> Handle(
        GetWebhooksQuery query, CancellationToken ct)
    {
        var bt = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, query.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", query.BookingTypeSlug);

        var webhooks = await webhookRepo.ListAsync(tenantContext.TenantId, bt.Id, ct);
        return webhooks.Select(w => w.ToDto()).ToList();
    }
}
