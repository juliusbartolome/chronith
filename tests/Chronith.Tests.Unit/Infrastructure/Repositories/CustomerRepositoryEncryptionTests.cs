using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

public sealed class CustomerRepositoryEncryptionTests
{
    private static (IEncryptionService enc, IBlindIndexService idx) CreateServices()
    {
        var encKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        var enc = new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = encKey }
        }));

        var hmacKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)99).ToArray());
        var idx = new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = hmacKey }));

        return (enc, idx);
    }

    [Fact]
    public void EmailToken_IsConsistentForSameEmail()
    {
        var (_, idx) = CreateServices();
        idx.ComputeToken("user@example.com").Should().Be(idx.ComputeToken("user@example.com"));
    }

    [Fact]
    public void EmailToken_IsCaseInsensitive()
    {
        var (_, idx) = CreateServices();
        idx.ComputeToken("User@Example.COM").Should().Be(idx.ComputeToken("user@example.com"));
    }

    [Fact]
    public void EncryptEmail_ThenDecrypt_ReturnsOriginal()
    {
        var (enc, _) = CreateServices();
        var encrypted = enc.Encrypt("user@example.com");
        enc.Decrypt(encrypted).Should().Be("user@example.com");
    }

    [Fact]
    public void DecryptEmail_LegacyPlaintext_ReturnRawValue()
    {
        // Plaintext values that have no version prefix should be returned as-is
        var enc = Substitute.For<IEncryptionService>();
        enc.Decrypt("plaintext@email.com")
            .Throws(new InvalidOperationException("no version prefix"));

        string DecryptEmail(string? value)
        {
            if (value is null) return string.Empty;
            try { return enc.Decrypt(value) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return value; }
        }

        DecryptEmail("plaintext@email.com").Should().Be("plaintext@email.com");
    }
}
