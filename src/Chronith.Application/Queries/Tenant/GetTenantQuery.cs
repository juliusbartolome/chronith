using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Tenant;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetTenantQuery : IRequest<TenantDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetTenantHandler(
    ITenantContext tenantContext,
    ITenantRepository tenantRepo)
    : IRequestHandler<GetTenantQuery, TenantDto>
{
    public async Task<TenantDto> Handle(GetTenantQuery query, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetByIdAsync(tenantContext.TenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantContext.TenantId);
        return tenant.ToDto();
    }
}
