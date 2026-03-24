namespace Chronith.Domain.Models;

public sealed class Customer
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? PasswordHash { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Mobile { get; private set; }
    public string? ExternalId { get; private set; }
    public string AuthProvider { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public uint RowVersion { get; private set; }

    internal Customer() { }

    public static Customer Create(Guid tenantId, string email, string? passwordHash, string firstName,
        string lastName, string? mobile, string authProvider)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            Mobile = mobile,
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
        var spaceIndex = (name ?? string.Empty).IndexOf(' ');
        var firstName = spaceIndex > 0 ? name![..spaceIndex] : name ?? string.Empty;
        var lastName = spaceIndex > 0 ? name![(spaceIndex + 1)..] : string.Empty;

        return new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PasswordHash = null,
            FirstName = firstName,
            LastName = lastName,
            ExternalId = externalId,
            AuthProvider = authProvider,
            IsEmailVerified = true,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static Customer Hydrate(Guid id, Guid tenantId, string email, string? passwordHash,
        string firstName, string lastName, string? mobile, string? externalId, string authProvider,
        bool isEmailVerified, bool isActive, bool isDeleted, DateTimeOffset createdAt,
        DateTimeOffset? lastLoginAt, uint rowVersion) => new()
    {
        Id = id, TenantId = tenantId, Email = email, PasswordHash = passwordHash,
        FirstName = firstName, LastName = lastName, Mobile = mobile,
        ExternalId = externalId, AuthProvider = authProvider,
        IsEmailVerified = isEmailVerified, IsActive = isActive, IsDeleted = isDeleted,
        CreatedAt = createdAt, LastLoginAt = lastLoginAt, RowVersion = rowVersion
    };

    public void UpdateProfile(string firstName, string lastName, string? mobile)
    {
        FirstName = firstName;
        LastName = lastName;
        Mobile = mobile;
    }

    public void MarkEmailVerified() => IsEmailVerified = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SoftDelete() => IsDeleted = true;

    public void RecordLogin() => LastLoginAt = DateTimeOffset.UtcNow;
}
