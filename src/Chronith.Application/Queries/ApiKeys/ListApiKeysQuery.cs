using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.ApiKeys;

// ── Query ────────────────────────────────────────────────────────────────────

public sealed record ListApiKeysQuery : IRequest<IReadOnlyList<ApiKeyDto>>, IQuery;

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class ListApiKeysHandler(
    ITenantContext tenantContext,
    IApiKeyRepository apiKeyRepo)
    : IRequestHandler<ListApiKeysQuery, IReadOnlyList<ApiKeyDto>>
{
    public async Task<IReadOnlyList<ApiKeyDto>> Handle(ListApiKeysQuery query, CancellationToken ct)
    {
        var keys = await apiKeyRepo.ListByTenantAsync(tenantContext.TenantId, ct);

        return keys
            .Select(k => new ApiKeyDto(
                Id: k.Id,
                Description: k.Description,
                Role: string.Empty,
                IsRevoked: k.IsRevoked,
                CreatedAt: k.CreatedAt,
                LastUsedAt: k.LastUsedAt))
            .ToList();
    }
}
