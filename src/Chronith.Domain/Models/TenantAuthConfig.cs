namespace Chronith.Domain.Models;

public sealed class TenantAuthConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public bool AllowBuiltInAuth { get; private set; }
    public string? OidcIssuer { get; private set; }
    public string? OidcClientId { get; private set; }
    public string? OidcAudience { get; private set; }
    public bool MagicLinkEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal TenantAuthConfig() { }

    public static TenantAuthConfig Create(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenantAuthConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AllowBuiltInAuth = true,
            MagicLinkEnabled = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static TenantAuthConfig Hydrate(Guid id, Guid tenantId, bool allowBuiltInAuth,
        string? oidcIssuer, string? oidcClientId, string? oidcAudience, bool magicLinkEnabled,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) => new()
    {
        Id = id, TenantId = tenantId, AllowBuiltInAuth = allowBuiltInAuth,
        OidcIssuer = oidcIssuer, OidcClientId = oidcClientId, OidcAudience = oidcAudience,
        MagicLinkEnabled = magicLinkEnabled, CreatedAt = createdAt, UpdatedAt = updatedAt
    };

    public void Update(bool allowBuiltInAuth, string? oidcIssuer, string? oidcClientId,
        string? oidcAudience, bool magicLinkEnabled)
    {
        AllowBuiltInAuth = allowBuiltInAuth;
        OidcIssuer = oidcIssuer;
        OidcClientId = oidcClientId;
        OidcAudience = oidcAudience;
        MagicLinkEnabled = magicLinkEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
