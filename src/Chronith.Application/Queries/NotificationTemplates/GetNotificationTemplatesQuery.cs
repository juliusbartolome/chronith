using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.NotificationTemplates;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetNotificationTemplatesQuery : IRequest<IReadOnlyList<NotificationTemplateDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetNotificationTemplatesQueryHandler(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo)
    : IRequestHandler<GetNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateDto>>
{
    public async Task<IReadOnlyList<NotificationTemplateDto>> Handle(
        GetNotificationTemplatesQuery query, CancellationToken ct)
    {
        var templates = await templateRepo.GetAllAsync(tenantContext.TenantId, ct);
        return templates.Select(t => t.ToDto()).ToList();
    }
}
