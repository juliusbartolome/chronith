namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantApiKeyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
