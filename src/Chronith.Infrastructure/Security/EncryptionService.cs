using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption service.
/// Each encrypted value is formatted as base64(nonce[12] || ciphertext[n] || tag[16]).
/// </summary>
public sealed class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IOptions<EncryptionOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.EncryptionKey);
    }

    public string? Encrypt(string? plaintext)
    {
        if (plaintext is null) return null;
        if (plaintext.Length == 0) return string.Empty;

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Pack: nonce (12) + ciphertext (n) + tag (16)
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string? Decrypt(string? encoded)
    {
        if (encoded is null) return null;
        if (encoded.Length == 0) return string.Empty;

        var data = Convert.FromBase64String(encoded);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;  // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize;       // 16

        var nonce = data[..nonceSize];
        var tag = data[^tagSize..];
        var ciphertext = data[nonceSize..^tagSize];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext); // throws CryptographicException on tamper

        return Encoding.UTF8.GetString(plaintext);
    }
}
