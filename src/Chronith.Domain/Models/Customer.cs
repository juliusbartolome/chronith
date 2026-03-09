namespace Chronith.Domain.Models;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? ExternalId { get; private set; }
    public string AuthProvider { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public uint RowVersion { get; private set; }

    internal Customer() { }

    public static Customer Create(Guid tenantId, string email, string? passwordHash, string name,
        string? phone, string authProvider)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            Name = name,
            Phone = phone,
            AuthProvider = authProvider,
            IsEmailVerified = false,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Customer CreateOidc(Guid tenantId, string email, string name, string externalId,
        string authProvider)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PasswordHash = null,
            Name = name,
            ExternalId = externalId,
            AuthProvider = authProvider,
            IsEmailVerified = true,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static Customer Hydrate(Guid id, Guid tenantId, string email, string? passwordHash,
        string name, string? phone, string? externalId, string authProvider, bool isEmailVerified,
        bool isActive, bool isDeleted, DateTimeOffset createdAt, DateTimeOffset? lastLoginAt,
        uint rowVersion) => new()
    {
        Id = id, TenantId = tenantId, Email = email, PasswordHash = passwordHash,
        Name = name, Phone = phone, ExternalId = externalId, AuthProvider = authProvider,
        IsEmailVerified = isEmailVerified, IsActive = isActive, IsDeleted = isDeleted,
        CreatedAt = createdAt, LastLoginAt = lastLoginAt, RowVersion = rowVersion
    };

    public void UpdateProfile(string name, string? phone)
    {
        Name = name;
        Phone = phone;
    }

    public void MarkEmailVerified() => IsEmailVerified = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SoftDelete() => IsDeleted = true;

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;
}
