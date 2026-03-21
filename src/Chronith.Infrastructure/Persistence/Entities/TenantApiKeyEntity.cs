namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantApiKeyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    // API keys use IsRevoked (logical revocation) rather than IsDeleted (soft-delete).
    // Revoked keys are retained for audit purposes and can never be re-activated.
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
