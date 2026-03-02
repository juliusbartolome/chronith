namespace Chronith.Domain.Models;

public sealed class TenantApiKey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string KeyHash { get; init; } = string.Empty;   // SHA-256 hex of raw key
    public string Description { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool IsRevoked { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; private set; }

    public void Revoke() => IsRevoked = true;

    public void UpdateLastUsed(DateTimeOffset now)
    {
        if (LastUsedAt is null || now > LastUsedAt)
            LastUsedAt = now;
    }

    /// <summary>
    /// Generates a new raw API key and returns both the key and its hash.
    /// The raw key must be shown to the user once and never stored.
    /// </summary>
    public static (string RawKey, string KeyHash) GenerateKey()
    {
        var randomBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        var rawKey = $"cth_{Convert.ToBase64String(randomBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
        var hash = ComputeHash(rawKey);
        return (rawKey, hash);
    }

    public static string ComputeHash(string rawKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
