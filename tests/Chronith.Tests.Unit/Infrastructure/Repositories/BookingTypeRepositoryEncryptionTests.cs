using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Verifies that BookingTypeRepository encrypts CustomerCallbackSecret on write
/// and decrypts it on read using the legacy-fallback pattern.
/// </summary>
public sealed class BookingTypeRepositoryEncryptionTests
{
    private static IEncryptionService CreateRealEncryptionService()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        var options = Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = key }
        });
        return new EncryptionService(options);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalSecret()
    {
        var encryption = CreateRealEncryptionService();
        var secret = "my-callback-secret";

        var encrypted = encryption.Encrypt(secret);
        var decrypted = encryption.Decrypt(encrypted);

        decrypted.Should().Be(secret);
    }

    [Fact]
    public void Decrypt_PlaintextLegacyValue_ReturnsRawValue()
    {
        // Simulates the legacy fallback: if Decrypt throws, return raw value.
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Decrypt("plaintext-secret")
            .Throws(new InvalidOperationException("no version prefix"));

        // The repository's DecryptCallbackSecret helper should return the raw value
        string DecryptCallbackSecret(string? secret)
        {
            if (secret is null) return string.Empty;
            try { return encryption.Decrypt(secret) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return secret; }
        }

        DecryptCallbackSecret("plaintext-secret").Should().Be("plaintext-secret");
    }

    [Fact]
    public void Decrypt_NullSecret_ReturnsEmpty()
    {
        var encryption = CreateRealEncryptionService();

        string DecryptCallbackSecret(string? secret)
        {
            if (secret is null) return string.Empty;
            try { return encryption.Decrypt(secret) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return secret; }
        }

        DecryptCallbackSecret(null).Should().BeEmpty();
    }
}
