namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantAuthConfigEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public bool AllowBuiltInAuth { get; set; }
    public string? OidcIssuer { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcAudience { get; set; }
    public bool MagicLinkEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
