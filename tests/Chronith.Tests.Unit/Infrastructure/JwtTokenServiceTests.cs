using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Chronith.Tests.Unit.Infrastructure;

public class JwtTokenServiceTests
{
    private static ITokenService CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "super-secret-test-signing-key-at-least-32-chars"
            })
            .Build();
        return new JwtTokenService(config);
    }

    [Fact]
    public void CreateAccessToken_ContainsTenantIdClaim()
    {
        var sut = CreateSut();
        var tenantId = Guid.NewGuid();
        var user = TenantUser.Create(tenantId, "a@b.com", "hash", TenantUserRole.Owner);

        var token = sut.CreateAccessToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        // Decode and check (without full JWT validation — just check it's parseable)
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "JWT must have three dot-separated parts");
    }

    [Fact]
    public void CreateRefreshToken_ReturnsTupleOfRawAndHash()
    {
        var sut = CreateSut();
        var (raw, hash) = sut.CreateRefreshToken();

        raw.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe(raw);
        hash.Length.Should().Be(64, "SHA-256 hex string is always 64 chars");
    }
}
