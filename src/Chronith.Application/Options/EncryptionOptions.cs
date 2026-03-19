namespace Chronith.Application.Options;

public sealed class EncryptionOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// The version tag used for all new encryptions (e.g. "v1").
    /// Must be a key in <see cref="KeyVersions"/>.
    /// </summary>
    public string EncryptionKeyVersion { get; set; } = "v1";

    /// <summary>
    /// Map of version tag → Base64-encoded 32-byte AES-256-GCM key.
    /// Add new versions here when rotating; keep old versions until re-encryption completes.
    /// </summary>
    public Dictionary<string, string> KeyVersions { get; set; } = [];

    /// <summary>
    /// When set, the <see cref="EncryptionKeyRotationService"/> will migrate
    /// ciphertexts prefixed with this version to <see cref="EncryptionKeyVersion"/>.
    /// Remove this setting after rotation completes.
    /// </summary>
    public string? EncryptionRotationSourceVersion { get; set; }
}
