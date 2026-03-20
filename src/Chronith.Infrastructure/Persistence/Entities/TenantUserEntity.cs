using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantUserEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    /// <summary>AES-256-GCM ciphertext. Populated by migration service at startup.</summary>
    public string? EmailEncrypted { get; set; }
    /// <summary>HMAC-SHA256 token for equality lookup.</summary>
    public string? EmailToken { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public TenantUserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
