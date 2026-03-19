namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class CustomerEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    /// <summary>AES-256-GCM ciphertext. Populated by migration service at startup.</summary>
    public string? EmailEncrypted { get; set; }
    /// <summary>HMAC-SHA256 token for equality lookup. Populated by migration service.</summary>
    public string? EmailToken { get; set; }
    /// <summary>AES-256-GCM ciphertext of Phone. Nullable — same as Phone.</summary>
    public string? PhoneEncrypted { get; set; }
    public string? PasswordHash { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ExternalId { get; set; }
    public string AuthProvider { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public uint RowVersion { get; set; }
}
