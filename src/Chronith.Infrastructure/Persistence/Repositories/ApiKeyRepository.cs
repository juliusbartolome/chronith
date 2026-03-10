using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyRepository(ChronithDbContext db) : IApiKeyRepository
{
    public Task AddAsync(TenantApiKey key, CancellationToken ct)
    {
        var entity = MapToEntity(key);
        db.TenantApiKeys.Add(entity);
        // SaveChanges is called by the unit of work
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TenantApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var entities = await db.TenantApiKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<TenantApiKey?> GetByHashAsync(string keyHash, CancellationToken ct)
    {
        var entity = await db.TenantApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && !k.IsRevoked, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<TenantApiKey?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var entity = await db.TenantApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpdateAsync(TenantApiKey key, CancellationToken ct)
    {
        var entity = await db.TenantApiKeys.FindAsync([key.Id], ct)
            ?? throw new InvalidOperationException($"TenantApiKey {key.Id} not found");

        entity.IsRevoked = key.IsRevoked;
        entity.LastUsedAt = key.LastUsedAt;
        // SaveChanges is deferred to the caller's IUnitOfWork
    }

    public async Task UpdateLastUsedAtAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        await db.TenantApiKeys
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now), ct);
    }

    private static TenantApiKeyEntity MapToEntity(TenantApiKey d) => new()
    {
        Id = d.Id,
        TenantId = d.TenantId,
        KeyHash = d.KeyHash,
        Description = d.Description,
        Role = d.Role,
        IsRevoked = d.IsRevoked,
        CreatedAt = d.CreatedAt,
        LastUsedAt = d.LastUsedAt,
        ExpiresAt = d.ExpiresAt,
    };

    private static TenantApiKey MapToDomain(TenantApiKeyEntity e)
    {
        var domain = new TenantApiKey
        {
            Id = e.Id,
            TenantId = e.TenantId,
            KeyHash = e.KeyHash,
            Description = e.Description,
            Role = e.Role,
            CreatedAt = e.CreatedAt,
            ExpiresAt = e.ExpiresAt,
        };

        if (e.IsRevoked)
            domain.Revoke();

        if (e.LastUsedAt.HasValue)
            domain.UpdateLastUsed(e.LastUsedAt.Value);

        return domain;
    }
}
