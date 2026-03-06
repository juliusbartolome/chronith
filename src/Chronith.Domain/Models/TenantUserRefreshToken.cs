namespace Chronith.Domain.Models;

public sealed class TenantUserRefreshToken
{
    public Guid Id { get; private set; }
    public Guid TenantUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // For Infrastructure hydration
    internal TenantUserRefreshToken() { }

    public static TenantUserRefreshToken Create(Guid tenantUserId, string tokenHash, TimeSpan ttl)
    {
        return new TenantUserRefreshToken
        {
            Id = Guid.NewGuid(),
            TenantUserId = tenantUserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static TenantUserRefreshToken Hydrate(Guid id, Guid tenantUserId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset? usedAt, DateTimeOffset createdAt) => new()
    {
        Id = id, TenantUserId = tenantUserId, TokenHash = tokenHash,
        ExpiresAt = expiresAt, UsedAt = usedAt, CreatedAt = createdAt
    };

    public bool IsValid() => UsedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
