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

    [Fact]
    public void UpdateLastUsed_SetsLastUsedAt()
    {
        var key = new TenantApiKey();
        var now = DateTimeOffset.UtcNow;
        key.UpdateLastUsed(now);
        key.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public void UpdateLastUsed_OlderTimestamp_DoesNotRewind()
    {
        var key = new TenantApiKey();
        var now = DateTimeOffset.UtcNow;
        key.UpdateLastUsed(now);
        var older = now.AddMinutes(-10);
        key.UpdateLastUsed(older);
        key.LastUsedAt.Should().Be(now); // not rewound
    }

    [Fact]
    public void Scopes_DefaultsToEmpty()
    {
        var key = new TenantApiKey();
        key.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void Scopes_AreSameAsPassedIntoConstructor()
    {
        var scopes = new[] { ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead };
        var key = new TenantApiKey { Scopes = scopes };
        key.Scopes.Should().BeEquivalentTo(scopes);
    }

    [Fact]
    public void Scopes_IsReadOnly_CannotBeMutated()
    {
        var key = new TenantApiKey { Scopes = [ApiKeyScope.BookingsRead] };
        var act = () => ((System.Collections.Generic.IList<string>)key.Scopes).Add("injected");
        act.Should().Throw<NotSupportedException>();
    }
}
