namespace Chronith.Application.Options;

public sealed class EncryptionOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Base64-encoded 32-byte (256-bit) master key used for AES-256-GCM encryption.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
