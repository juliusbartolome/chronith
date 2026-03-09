using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Audit;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetAuditEntriesQuery(
    string? EntityType = null,
    Guid? EntityId = null,
    string? UserId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResultDto<AuditEntryDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetAuditEntriesHandler(
    ITenantContext tenantContext,
    IAuditEntryRepository auditRepo)
    : IRequestHandler<GetAuditEntriesQuery, PagedResultDto<AuditEntryDto>>
{
    public async Task<PagedResultDto<AuditEntryDto>> Handle(GetAuditEntriesQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await auditRepo.QueryAsync(
            tenantContext.TenantId,
            query.EntityType,
            query.EntityId,
            query.UserId,
            query.Action,
            query.From,
            query.To,
            query.Page,
            query.PageSize,
            ct);

        return new PagedResultDto<AuditEntryDto>(
            Items: items.Select(e => e.ToDto()).ToList(),
            TotalCount: totalCount,
            Page: query.Page,
            PageSize: query.PageSize);
    }
}
