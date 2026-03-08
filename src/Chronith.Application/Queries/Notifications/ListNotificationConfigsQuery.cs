using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Notifications;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListNotificationConfigsQuery
    : IRequest<IReadOnlyList<TenantNotificationConfigDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListNotificationConfigsHandler(
    ITenantContext tenantContext,
    INotificationConfigRepository configRepo)
    : IRequestHandler<ListNotificationConfigsQuery, IReadOnlyList<TenantNotificationConfigDto>>
{
    public async Task<IReadOnlyList<TenantNotificationConfigDto>> Handle(
        ListNotificationConfigsQuery query, CancellationToken ct)
    {
        var configs = await configRepo.ListByTenantAsync(tenantContext.TenantId, ct);
        return configs.Select(c => c.ToDto()).ToList();
    }
}
