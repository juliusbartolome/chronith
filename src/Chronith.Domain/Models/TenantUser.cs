using Chronith.Domain.Enums;

namespace Chronith.Domain.Models;

public sealed class TenantUser
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public TenantUserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // For Infrastructure hydration
    internal TenantUser() { }

    public static TenantUser Create(Guid tenantId, string email, string passwordHash, TenantUserRole role)
    {
        return new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static TenantUser Hydrate(Guid id, Guid tenantId, string email, string passwordHash,
        TenantUserRole role, bool isActive, DateTimeOffset createdAt) => new()
    {
        Id = id, TenantId = tenantId, Email = email, PasswordHash = passwordHash,
        Role = role, IsActive = isActive, CreatedAt = createdAt
    };

    /// <summary>
    /// Maps Role to the authorization role string used by existing endpoint policies.
    /// Owner and Admin → "TenantAdmin"; Member → "TenantStaff".
    /// </summary>
    public string AuthorizationRole => Role switch
    {
        TenantUserRole.Owner or TenantUserRole.Admin => "TenantAdmin",
        TenantUserRole.Member => "TenantStaff",
        _ => throw new InvalidOperationException($"Unknown role: {Role}")
    };

    public void UpdateEmail(string email) => Email = email.ToLowerInvariant();
    public void UpdatePasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
