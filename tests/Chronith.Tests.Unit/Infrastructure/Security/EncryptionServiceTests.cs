using System.Security.Cryptography;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public class EncryptionServiceTests
{
    private static IEncryptionService CreateSut()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKey = Convert.ToBase64String(key)
        });
        return new EncryptionService(options);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var sut = CreateSut();
        const string plaintext = "smtp-password-secret-value-123!";

        var ciphertext = sut.Encrypt(plaintext);
        var result = sut.Decrypt(ciphertext);

        result.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertexts()
    {
        var sut = CreateSut();
        const string plaintext = "same-plaintext-different-nonce";

        var first = sut.Encrypt(plaintext);
        var second = sut.Encrypt(plaintext);

        first.Should().NotBe(second, "each encryption uses a unique random nonce");
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var sut = CreateSut();
        const string plaintext = "sensitive-data";

        var ciphertext = sut.Encrypt(plaintext)!;

        // Tamper: flip the last byte of the base64 payload
        var bytes = Convert.FromBase64String(ciphertext);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        var act = () => sut.Decrypt(tampered);
        act.Should().Throw<CryptographicException>("AES-GCM tag verification must fail on tampered data");
    }

    [Fact]
    public void Encrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        var result = sut.Encrypt(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Decrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        var result = sut.Decrypt(null);
        result.Should().BeNull();
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var result = sut.Encrypt(string.Empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var result = sut.Decrypt(string.Empty);
        result.Should().BeEmpty();
    }
}
