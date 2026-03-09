using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CustomerRefreshTokenTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var customerId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var token = CustomerRefreshToken.Create(customerId, "token-hash-abc", TimeSpan.FromDays(7));

        token.Id.Should().NotBeEmpty();
        token.CustomerId.Should().Be(customerId);
        token.TokenHash.Should().Be("token-hash-abc");
        token.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromSeconds(2));
        token.UsedAt.Should().BeNull();
        token.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void IsValid_WhenNotUsedAndNotExpired_ReturnsTrue()
    {
        var token = CustomerRefreshToken.Create(Guid.NewGuid(), "hash", TimeSpan.FromHours(1));

        token.IsValid().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenUsed_ReturnsFalse()
    {
        var token = CustomerRefreshToken.Create(Guid.NewGuid(), "hash", TimeSpan.FromHours(1));

        token.MarkUsed();

        token.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        var token = CustomerRefreshToken.Hydrate(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            tokenHash: "hash",
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1),
            usedAt: null,
            createdAt: DateTimeOffset.UtcNow.AddHours(-2));

        token.IsValid().Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_SetsUsedAt()
    {
        var token = CustomerRefreshToken.Create(Guid.NewGuid(), "hash", TimeSpan.FromHours(1));

        token.UsedAt.Should().BeNull();

        var before = DateTimeOffset.UtcNow;
        token.MarkUsed();

        token.UsedAt.Should().NotBeNull();
        token.UsedAt.Should().BeOnOrAfter(before);
    }
}
