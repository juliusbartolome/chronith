using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantSettings;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetTenantSettingsQuery : IRequest<TenantSettingsDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetTenantSettingsHandler(
    ITenantContext tenantContext,
    ITenantSettingsRepository settingsRepo)
    : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery query, CancellationToken ct)
    {
        var settings = await settingsRepo.GetOrCreateAsync(tenantContext.TenantId, ct);
        return settings.ToDto();
    }
}
