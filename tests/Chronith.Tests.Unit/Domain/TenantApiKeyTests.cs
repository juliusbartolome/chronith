using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantApiKeyTests
{
    [Fact]
    public void GenerateKey_RawKey_StartsWithPrefix()
    {
        var (rawKey, _) = TenantApiKey.GenerateKey();
        rawKey.Should().StartWith("cth_");
    }

    [Fact]
    public void GenerateKey_Hash_MatchesComputeHash()
    {
        var (rawKey, hash) = TenantApiKey.GenerateKey();
        TenantApiKey.ComputeHash(rawKey).Should().Be(hash);
    }

    [Fact]
    public void GenerateKey_TwoKeys_AreUnique()
    {
        var (key1, _) = TenantApiKey.GenerateKey();
        var (key2, _) = TenantApiKey.GenerateKey();
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void Revoke_SetsIsRevoked()
    {
        var key = new TenantApiKey();
        key.IsRevoked.Should().BeFalse();
        key.Revoke();
        key.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void ComputeHash_SameInput_SameOutput()
    {
        var hash1 = TenantApiKey.ComputeHash("cth_test");
        var hash2 = TenantApiKey.ComputeHash("cth_test");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_DifferentOutput()
    {
        var hash1 = TenantApiKey.ComputeHash("cth_test1");
        var hash2 = TenantApiKey.ComputeHash("cth_test2");
        hash1.Should().NotBe(hash2);
    }
}
