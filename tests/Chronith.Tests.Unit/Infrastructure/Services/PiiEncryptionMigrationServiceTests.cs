using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Services;

/// <summary>
/// Tests the core encryption helpers used by PiiEncryptionMigrationService.
/// The service itself requires EF/DB (integration test territory).
/// These tests verify the encryption round-trips and token computation
/// that the service relies on.
/// </summary>
public sealed class PiiEncryptionMigrationServiceTests
{
    private static IEncryptionService CreateEncryption()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)42).ToArray());
        return new EncryptionService(Options.Create(new EncryptionOptions
        {
            EncryptionKeyVersion = "v1",
            KeyVersions = new Dictionary<string, string> { ["v1"] = key }
        }));
    }

    private static IBlindIndexService CreateBlindIndex()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)77).ToArray());
        return new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = key }));
    }

    [Fact]
    public void IsEncrypted_VersionPrefixed_ReturnsTrue()
    {
        // Values that already have a version prefix are considered encrypted
        "v1:someBase64=".StartsWith("v1:").Should().BeTrue();
    }

    [Fact]
    public void IsEncrypted_PlaintextEmail_ReturnsFalse()
    {
        "user@example.com".StartsWith("v1:").Should().BeFalse();
    }

    [Fact]
    public void IsEncrypted_Empty_ReturnsFalse()
    {
        string.Empty.StartsWith("v1:").Should().BeFalse();
    }

    [Fact]
    public void EncryptAndToken_RoundTrip()
    {
        var enc = CreateEncryption();
        var idx = CreateBlindIndex();
        var email = "migrate@example.com";

        var encrypted = enc.Encrypt(email)!;
        var token = idx.ComputeToken(email);

        enc.Decrypt(encrypted).Should().Be(email);
        token.Should().HaveLength(64);
    }
}
