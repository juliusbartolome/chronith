using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.NotificationTemplates;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetNotificationTemplateByIdQuery(Guid Id)
    : IRequest<NotificationTemplateDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetNotificationTemplateByIdQueryHandler(
    ITenantContext tenantContext,
    INotificationTemplateRepository templateRepo)
    : IRequestHandler<GetNotificationTemplateByIdQuery, NotificationTemplateDto>
{
    public async Task<NotificationTemplateDto> Handle(
        GetNotificationTemplateByIdQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(tenantContext.TenantId, query.Id, ct)
            ?? throw new NotFoundException("NotificationTemplate", query.Id);

        return template.ToDto();
    }
}
