using System.Security.Cryptography;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public class EncryptionServiceTests
{
    private static string NewBase64Key()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    private static IEncryptionService CreateSut(
        string currentVersion = "v1",
        Dictionary<string, string>? extraVersions = null)
    {
        var versions = new Dictionary<string, string> { [currentVersion] = NewBase64Key() };
        if (extraVersions is not null)
            foreach (var (k, v) in extraVersions) versions[k] = v;

        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = currentVersion,
            KeyVersions = versions
        });
        return new EncryptionService(options);
    }

    // ── round-trip ────────────────────────────────────────────────────────────

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

    // ── version prefix ────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_ProducesCiphertextWithVersionPrefix()
    {
        var sut = CreateSut(currentVersion: "v1");
        var ciphertext = sut.Encrypt("any-plaintext");
        ciphertext.Should().StartWith("v1:", "ciphertext must carry the key version");
    }

    [Fact]
    public void Encrypt_WithDifferentVersion_UsesCorrectPrefix()
    {
        var sut = CreateSut(currentVersion: "v2");
        var ciphertext = sut.Encrypt("any-plaintext");
        ciphertext.Should().StartWith("v2:");
    }

    // ── multi-version decryption ──────────────────────────────────────────────

    [Fact]
    public void Decrypt_OldVersionCiphertext_DecryptsWithOldKey()
    {
        // Produce a v1 ciphertext
        var v1Key = NewBase64Key();
        var v1Sut = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = v1Key }
        });
        var v1Service = new EncryptionService(v1Sut);
        var v1Ciphertext = v1Service.Encrypt("secret-data")!;

        // A service with both v1 and v2 should still decrypt the v1 ciphertext
        var v2Key = NewBase64Key();
        var bothOptions = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v2",
            KeyVersions = new Dictionary<string, string> { ["v1"] = v1Key, ["v2"] = v2Key }
        });
        var bothService = new EncryptionService(bothOptions);

        var result = bothService.Decrypt(v1Ciphertext);
        result.Should().Be("secret-data");
    }

    // ── tamper detection ──────────────────────────────────────────────────────

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var sut = CreateSut();
        var ciphertext = sut.Encrypt("sensitive-data")!;

        // Strip the version prefix, tamper with the base64 payload, reattach prefix
        var colonIdx = ciphertext.IndexOf(':');
        var version = ciphertext[..colonIdx];
        var bytes = Convert.FromBase64String(ciphertext[(colonIdx + 1)..]);
        bytes[^1] ^= 0xFF;
        var tampered = $"{version}:{Convert.ToBase64String(bytes)}";

        var act = () => sut.Decrypt(tampered);
        act.Should().Throw<CryptographicException>("AES-GCM tag verification must fail on tampered data");
    }

    // ── null / empty passthrough ──────────────────────────────────────────────

    [Fact]
    public void Encrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        sut.Encrypt(null).Should().BeNull();
    }

    [Fact]
    public void Decrypt_NullInput_ReturnsNull()
    {
        var sut = CreateSut();
        sut.Decrypt(null).Should().BeNull();
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        sut.Encrypt(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmptyString()
    {
        var sut = CreateSut();
        sut.Decrypt(string.Empty).Should().BeEmpty();
    }

    // ── error cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenKeyIsNot32Bytes()
    {
        var shortKey = Convert.ToBase64String(new byte[16]);
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = shortKey }
        });

        var act = () => new EncryptionService(options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenCurrentVersionNotInKeyVersions()
    {
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v2",
            KeyVersions = new Dictionary<string, string> { ["v1"] = NewBase64Key() }
        });

        var act = () => new EncryptionService(options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*v2*");
    }

    [Fact]
    public void Decrypt_UnknownVersionPrefix_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(currentVersion: "v1");
        // Manually craft a ciphertext with an unknown version
        var act = () => sut.Decrypt("v99:SGVsbG8=");
        act.Should().Throw<InvalidOperationException>().WithMessage("*v99*");
    }

    [Fact]
    public void Decrypt_LegacyUnversionedCiphertext_ThrowsInvalidOperationException()
    {
        // A ciphertext with no "version:" prefix (old format before this change)
        // must fail with a clear error rather than a confusing CryptographicException
        var sut = CreateSut();
        var act = () => sut.Decrypt("SGVsbG8gV29ybGQ=");
        act.Should().Throw<InvalidOperationException>().WithMessage("*version*");
    }

    [Fact]
    public void Decrypt_TooShortPayload_ThrowsInvalidOperationException()
    {
        var sut = CreateSut(currentVersion: "v1");
        // Valid Base64, known version prefix, but payload decodes to fewer than 28 bytes
        var act = () => sut.Decrypt("v1:AAAA");
        act.Should().Throw<InvalidOperationException>().WithMessage("*too short*");
    }
}
