using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

internal static class TenantUserEntityMapper
{
    public static TenantUserEntity ToEntity(this TenantUser u) => new()
    {
        Id = u.Id,
        TenantId = u.TenantId,
        Email = u.Email,
        PasswordHash = u.PasswordHash,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };

    public static TenantUser ToDomain(this TenantUserEntity e) =>
        TenantUser.Hydrate(e.Id, e.TenantId, e.Email, e.PasswordHash, e.Role, e.IsActive, e.CreatedAt);

    public static TenantUserRefreshTokenEntity ToEntity(this TenantUserRefreshToken t) => new()
    {
        Id = t.Id,
        TenantUserId = t.TenantUserId,
        TokenHash = t.TokenHash,
        ExpiresAt = t.ExpiresAt,
        UsedAt = t.UsedAt,
        CreatedAt = t.CreatedAt
    };

    public static TenantUserRefreshToken ToDomain(this TenantUserRefreshTokenEntity e) =>
        TenantUserRefreshToken.Hydrate(e.Id, e.TenantUserId, e.TokenHash, e.ExpiresAt, e.UsedAt, e.CreatedAt);
}
