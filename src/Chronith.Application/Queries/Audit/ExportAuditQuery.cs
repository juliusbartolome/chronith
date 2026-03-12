using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Audit;

public sealed record ExportAuditQuery(
    DateTimeOffset? From,
    DateTimeOffset? To)
    : IRequest<ExportFileResult>, IQuery;

public sealed class ExportAuditQueryHandler(
    ITenantContext tenantContext,
    IAuditEntryRepository auditRepo,
    ICsvExportService csvService)
    : IRequestHandler<ExportAuditQuery, ExportFileResult>
{
    private const int MaxRows = 5_000;

    public async Task<ExportFileResult> Handle(ExportAuditQuery query, CancellationToken ct)
    {
        var (items, _) = await auditRepo.QueryAsync(
            tenantContext.TenantId,
            entityType: null, entityId: null, userId: null, action: null,
            query.From, query.To,
            page: 1, pageSize: MaxRows, ct);

        var rows = items.Select(e => new AuditExportRowDto(
            e.Id, e.Timestamp, e.Action, e.EntityType, e.EntityId,
            e.UserId, e.UserRole)).ToList();

        var from = (query.From ?? DateTimeOffset.UtcNow.AddMonths(-1)).ToString("yyyyMMdd");
        var to = (query.To ?? DateTimeOffset.UtcNow).ToString("yyyyMMdd");

        var csv = csvService.GenerateAuditCsv(rows);
        return new ExportFileResult(csv, "text/csv", $"audit-{from}-{to}.csv");
    }
}
