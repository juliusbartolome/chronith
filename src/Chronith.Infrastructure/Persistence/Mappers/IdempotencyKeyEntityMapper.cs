using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

internal static class IdempotencyKeyEntityMapper
{
    public static IdempotencyKeyEntity ToEntity(this IdempotencyKey k) => new()
    {
        Id = k.Id,
        TenantId = k.TenantId,
        Key = k.Key,
        EndpointRoute = k.EndpointRoute,
        RequestHash = k.RequestHash,
        ResponseStatusCode = k.ResponseStatusCode,
        ResponseBody = k.ResponseBody,
        CreatedAt = k.CreatedAt,
        ExpiresAt = k.ExpiresAt
    };

    public static IdempotencyKey ToDomain(this IdempotencyKeyEntity e) =>
        IdempotencyKey.Hydrate(
            e.Id, e.TenantId, e.Key, e.EndpointRoute,
            e.RequestHash, e.ResponseStatusCode, e.ResponseBody,
            e.CreatedAt, e.ExpiresAt);
}
