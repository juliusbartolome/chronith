using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Audit;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetAuditEntryByIdQuery(Guid Id) : IRequest<AuditEntryDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetAuditEntryByIdHandler(
    ITenantContext tenantContext,
    IAuditEntryRepository auditRepo)
    : IRequestHandler<GetAuditEntryByIdQuery, AuditEntryDto>
{
    public async Task<AuditEntryDto> Handle(GetAuditEntryByIdQuery query, CancellationToken ct)
    {
        var entry = await auditRepo.GetByIdAsync(tenantContext.TenantId, query.Id, ct)
            ?? throw new NotFoundException("AuditEntry", query.Id);

        return entry.ToDto();
    }
}
