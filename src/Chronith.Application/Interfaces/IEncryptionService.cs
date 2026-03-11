namespace Chronith.Application.Interfaces;

/// <summary>
/// Provides AES-256-GCM authenticated encryption/decryption for sensitive at-rest data.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext string. Returns <see langword="null"/> if <paramref name="plaintext"/> is <see langword="null"/>,
    /// or an empty string if <paramref name="plaintext"/> is empty.
    /// </summary>
    string? Encrypt(string? plaintext);

    /// <summary>
    /// Decrypts a previously encrypted ciphertext. Returns <see langword="null"/> if <paramref name="ciphertext"/> is <see langword="null"/>,
    /// or an empty string if <paramref name="ciphertext"/> is empty.
    /// Throws <see cref="System.Security.Cryptography.CryptographicException"/> if the ciphertext has been tampered with.
    /// </summary>
    string? Decrypt(string? ciphertext);
}
