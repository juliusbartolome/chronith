namespace Chronith.Infrastructure.Security;

public sealed class BlindIndexOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Base64-encoded 32-byte (256-bit) HMAC-SHA256 key.
    /// Separate from the AES encryption key.
    /// </summary>
    public string HmacKey { get; set; } = string.Empty;
}
