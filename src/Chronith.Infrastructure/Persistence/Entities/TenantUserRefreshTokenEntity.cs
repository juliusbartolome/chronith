namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantUserRefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid TenantUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public TenantUserEntity TenantUser { get; set; } = null!;
}
