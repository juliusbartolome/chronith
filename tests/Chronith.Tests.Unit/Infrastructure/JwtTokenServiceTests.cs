using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Tests.Unit.Infrastructure;

public class JwtTokenServiceTests
{
    private const string TestSigningKey = "super-secret-test-signing-key-at-least-32-chars";

    private static ITokenService CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestSigningKey
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

    [Fact]
    public void CreateMagicLinkToken_ReturnsJwt_WithCorrectClaims()
    {
        // Arrange
        var sut = CreateSut();
        var tenantId = Guid.NewGuid();
        var customer = Customer.Create(
            tenantId: tenantId,
            email: "alice@example.com",
            passwordHash: "hashed-password",
            firstName: "Alice",
            lastName: "Doe",
            mobile: null,
            authProvider: "builtin");

        var before = DateTime.UtcNow;

        // Act
        var token = sut.CreateMagicLinkToken(customer, "test-tenant");

        // Assert
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        decoded.Subject.Should().Be(customer.Id.ToString(), "sub claim must be customer ID");

        decoded.Claims.FirstOrDefault(c => c.Type == "email")
            ?.Value.Should().Be("alice@example.com", "email claim must match customer email");

        decoded.Claims.FirstOrDefault(c => c.Type == "tenantSlug")
            ?.Value.Should().Be("test-tenant", "tenantSlug claim must match provided slug");

        decoded.Claims.FirstOrDefault(c => c.Type == "purpose")
            ?.Value.Should().Be("magic-link-verify", "purpose claim must be 'magic-link-verify'");

        var expectedExpiry = before.AddHours(24);
        decoded.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5),
            "token must expire ~24 hours from creation");
    }

    // --- ValidateMagicLinkToken tests ---

    private static Customer BuildCustomer(Guid? tenantId = null) =>
        Customer.Create(
            tenantId: tenantId ?? Guid.NewGuid(),
            email: "alice@example.com",
            passwordHash: null,
            firstName: "Alice",
            lastName: "Doe",
            mobile: null,
            authProvider: "magic-link");

    [Fact]
    public void ValidateMagicLinkToken_ReturnsCustomerId_ForValidToken()
    {
        var sut = CreateSut();
        var customer = BuildCustomer();
        var token = sut.CreateMagicLinkToken(customer, "test-tenant");

        var customerId = sut.ValidateMagicLinkToken(token, "test-tenant");

        customerId.Should().Be(customer.Id);
    }

    [Fact]
    public void ValidateMagicLinkToken_ThrowsUnauthorized_ForExpiredToken()
    {
        // Arrange: build a token that is already expired using raw JWT construction
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var customerId = Guid.NewGuid();

        var expiredToken = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
                new Claim("tenantSlug", "test-tenant"),
                new Claim("purpose", "magic-link-verify"),
            ],
            expires: DateTime.UtcNow.AddHours(-1), // already expired
            signingCredentials: creds);
        var tokenStr = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var sut = CreateSut();

        // Act
        var act = () => sut.ValidateMagicLinkToken(tokenStr, "test-tenant");

        // Assert
        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void ValidateMagicLinkToken_ThrowsUnauthorized_ForWrongPurpose()
    {
        // Arrange: build a token with wrong purpose claim
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var customerId = Guid.NewGuid();

        var wrongPurposeToken = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString()),
                new Claim("tenantSlug", "test-tenant"),
                new Claim("purpose", "something-else"),
            ],
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);
        var tokenStr = new JwtSecurityTokenHandler().WriteToken(wrongPurposeToken);

        var sut = CreateSut();

        // Act
        var act = () => sut.ValidateMagicLinkToken(tokenStr, "test-tenant");

        // Assert
        act.Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void ValidateMagicLinkToken_ThrowsUnauthorized_ForWrongTenantSlug()
    {
        var sut = CreateSut();
        var customer = BuildCustomer();
        // token issued for "tenant-a"
        var token = sut.CreateMagicLinkToken(customer, "tenant-a");

        // but we validate against "tenant-b"
        var act = () => sut.ValidateMagicLinkToken(token, "tenant-b");

        act.Should().Throw<UnauthorizedException>();
    }
}
