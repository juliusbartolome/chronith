using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

internal static class TenantAuthConfigEntityMapper
{
    public static TenantAuthConfigEntity ToEntity(this TenantAuthConfig c) => new()
    {
        Id = c.Id,
        TenantId = c.TenantId,
        AllowBuiltInAuth = c.AllowBuiltInAuth,
        OidcIssuer = c.OidcIssuer,
        OidcClientId = c.OidcClientId,
        OidcAudience = c.OidcAudience,
        MagicLinkEnabled = c.MagicLinkEnabled,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    public static TenantAuthConfig ToDomain(this TenantAuthConfigEntity e) =>
        TenantAuthConfig.Hydrate(
            e.Id, e.TenantId, e.AllowBuiltInAuth,
            e.OidcIssuer, e.OidcClientId, e.OidcAudience,
            e.MagicLinkEnabled, e.CreatedAt, e.UpdatedAt);
}
