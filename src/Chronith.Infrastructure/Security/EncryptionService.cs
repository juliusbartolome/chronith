using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption service with key versioning.
///
/// Ciphertext format: {version}:{base64(nonce[12] || ciphertext[n] || tag[16])}
///
/// Multiple key versions may coexist. <see cref="EncryptionOptions.EncryptionKeyVersion"/>
/// determines which key is used for new encryptions. Decryption inspects the version
/// prefix and selects the matching key, so old ciphertexts remain readable while
/// <see cref="EncryptionKeyRotationService"/> migrates them in the background.
/// </summary>
public sealed class EncryptionService : IEncryptionService
{
    private readonly string _currentVersion;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public EncryptionService(IOptions<EncryptionOptions> options)
    {
        var opts = options.Value;
        _currentVersion = opts.EncryptionKeyVersion;

        if (opts.KeyVersions is not { Count: > 0 })
            throw new InvalidOperationException(
                "EncryptionOptions.KeyVersions must contain at least one entry.");

        if (!opts.KeyVersions.ContainsKey(_currentVersion))
            throw new InvalidOperationException(
                $"EncryptionOptions.EncryptionKeyVersion '{_currentVersion}' " +
                $"is not present in KeyVersions.");

        var keys = new Dictionary<string, byte[]>(opts.KeyVersions.Count);
        foreach (var (version, b64Key) in opts.KeyVersions)
        {
            var keyBytes = Convert.FromBase64String(b64Key);
            if (keyBytes.Length != 32)
                throw new InvalidOperationException(
                    $"Key for version '{version}' must be exactly 32 bytes (256-bit). " +
                    $"Got {keyBytes.Length} bytes.");
            keys[version] = keyBytes;
        }
        _keys = keys;
    }

    public string? Encrypt(string? plaintext)
    {
        if (plaintext is null) return null;
        if (plaintext.Length == 0) return string.Empty;

        var key = _keys[_currentVersion];

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);

        // Pack: nonce (12) + ciphertext (n) + tag (16)
        var packed = new byte[nonce.Length + ciphertextBytes.Length + tag.Length];
        nonce.CopyTo(packed, 0);
        ciphertextBytes.CopyTo(packed, nonce.Length);
        tag.CopyTo(packed, nonce.Length + ciphertextBytes.Length);

        return $"{_currentVersion}:{Convert.ToBase64String(packed)}";
    }

    public string? Decrypt(string? encoded)
    {
        if (encoded is null) return null;
        if (encoded.Length == 0) return string.Empty;

        var colonIdx = encoded.IndexOf(':');
        if (colonIdx <= 0)
            throw new InvalidOperationException(
                $"Ciphertext has no version prefix. Expected format: '{{version}}:{{base64}}'. " +
                $"This ciphertext was produced before key versioning was introduced and cannot " +
                $"be decrypted (data loss accepted during initial v1 rotation).");

        var version = encoded[..colonIdx];
        var payload = encoded[(colonIdx + 1)..];

        if (!_keys.TryGetValue(version, out var key))
            throw new InvalidOperationException(
                $"Unknown encryption key version '{version}'. " +
                $"Add this version to Security:KeyVersions configuration.");

        var data = Convert.FromBase64String(payload);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;  // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize;       // 16

        if (data.Length < nonceSize + tagSize)
            throw new InvalidOperationException(
                $"Ciphertext payload is too short ({data.Length} bytes). " +
                $"Expected at least {nonceSize + tagSize} bytes.");

        var nonce = data[..nonceSize];
        var tag = data[^tagSize..];
        var ciphertext = data[nonceSize..^tagSize];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws CryptographicException on tamper

        return Encoding.UTF8.GetString(plaintext);
    }
}
