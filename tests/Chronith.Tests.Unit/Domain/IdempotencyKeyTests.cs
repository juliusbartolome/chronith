using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class IdempotencyKeyTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var key = "idem-key-123";
        var endpointRoute = "POST /v1/booking-types/test/bookings";
        var requestHash = "abc123hash";
        var responseStatusCode = 201;
        var responseBody = """{"id":"some-id"}""";
        var ttl = TimeSpan.FromHours(24);
        var before = DateTimeOffset.UtcNow;

        // Act
        var idempotencyKey = IdempotencyKey.Create(
            tenantId, key, endpointRoute, requestHash,
            responseStatusCode, responseBody, ttl);

        var after = DateTimeOffset.UtcNow;

        // Assert
        idempotencyKey.Id.Should().NotBeEmpty();
        idempotencyKey.TenantId.Should().Be(tenantId);
        idempotencyKey.Key.Should().Be(key);
        idempotencyKey.EndpointRoute.Should().Be(endpointRoute);
        idempotencyKey.RequestHash.Should().Be(requestHash);
        idempotencyKey.ResponseStatusCode.Should().Be(responseStatusCode);
        idempotencyKey.ResponseBody.Should().Be(responseBody);
        idempotencyKey.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        idempotencyKey.ExpiresAt.Should().BeOnOrAfter(before.Add(ttl)).And.BeOnOrBefore(after.Add(ttl));
    }

    [Fact]
    public void MatchesRequest_ReturnsTrueForSameHash()
    {
        // Arrange
        var hash = "same-hash-value";
        var idempotencyKey = IdempotencyKey.Create(
            Guid.NewGuid(), "key", "route", hash, 201, "{}", TimeSpan.FromHours(1));

        // Act
        var result = idempotencyKey.MatchesRequest(hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesRequest_ReturnsFalseForDifferentHash()
    {
        // Arrange
        var idempotencyKey = IdempotencyKey.Create(
            Guid.NewGuid(), "key", "route", "original-hash", 201, "{}", TimeSpan.FromHours(1));

        // Act
        var result = idempotencyKey.MatchesRequest("different-hash");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ReturnsTrueWhenPastExpiresAt()
    {
        // Arrange — use Hydrate to create a key with an already-expired ExpiresAt
        var expiredKey = IdempotencyKey.Hydrate(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            key: "expired-key",
            endpointRoute: "route",
            requestHash: "hash",
            responseStatusCode: 200,
            responseBody: "{}",
            createdAt: DateTimeOffset.UtcNow.AddHours(-2),
            expiresAt: DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var result = expiredKey.IsExpired();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ReturnsFalseWhenNotExpired()
    {
        // Arrange
        var idempotencyKey = IdempotencyKey.Create(
            Guid.NewGuid(), "key", "route", "hash", 200, "{}", TimeSpan.FromHours(24));

        // Act
        var result = idempotencyKey.IsExpired();

        // Assert
        result.Should().BeFalse();
    }
}
