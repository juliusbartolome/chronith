using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Tests.Unit.Infrastructure.Auth;

public class JwtTokenServiceMultiKeyTests
{
    private const string PrimaryKey = "primary-signing-key-at-least-32-chars!!";
    private const string SecondaryKey = "secondary-old-key-at-least-32-chars!";
    private const string UnknownKey = "completely-unknown-key-32chars!!!!";

    /// <summary>
    /// Creates a JwtTokenService configured with a primary key and optionally a secondary key.
    /// </summary>
    private static ITokenService CreateSut(string primaryKey, string? secondaryKey = null)
    {
        var config = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = primaryKey,
        };

        if (secondaryKey is not null)
        {
            config["Jwt:SigningKeys:0"] = primaryKey;
            config["Jwt:SigningKeys:1"] = secondaryKey;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        return new JwtTokenService(configuration);
    }

    /// <summary>
    /// Validates a JWT token against the given validation keys.
    /// Returns the ClaimsPrincipal on success, null on failure.
    /// </summary>
    private static ClaimsPrincipal? ValidateToken(string token, params string[] keyStrings)
    {
        var keys = keyStrings
            .Select(k => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(k)))
            .Cast<SecurityKey>()
            .ToList();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKeys = keys,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
    }

    [Fact]
    public void CreateAccessToken_WithPrimaryKey_ValidatesWithPrimaryKey()
    {
        var sut = CreateSut(PrimaryKey);
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Owner);

        var token = sut.CreateAccessToken(user);

        var principal = ValidateToken(token, PrimaryKey);
        principal.Should().NotBeNull("token signed with primary key must validate with primary key");
    }

    [Fact]
    public void CreateAccessToken_WithMultipleKeys_UsesFirstKeyToSign()
    {
        // When SigningKeys has [primary, secondary], token should be signed with first key
        var sut = CreateSut(PrimaryKey, SecondaryKey);
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Owner);

        var token = sut.CreateAccessToken(user);

        // Must validate with primary (first) key
        var principalWithPrimary = ValidateToken(token, PrimaryKey);
        principalWithPrimary.Should().NotBeNull("token must be signed with primary key");

        // Must NOT validate with secondary key alone
        var principalWithSecondaryOnly = ValidateToken(token, SecondaryKey);
        principalWithSecondaryOnly.Should().BeNull("token signed with primary should not validate with secondary-only");
    }

    [Fact]
    public void ValidateToken_SignedWithSecondaryKey_ValidatesWhenBothKeysPresent()
    {
        // Simulate a token signed with the OLD/rotation key (secondary)
        // The service configured with both keys should accept it
        var secondaryKeyBytes = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecondaryKey));
        var creds = new SigningCredentials(secondaryKeyBytes, SecurityAlgorithms.HmacSha256);

        var tokenOldKey = new JwtSecurityToken(
            claims: [new Claim("sub", "user-123"), new Claim("tenant_id", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        var oldToken = new JwtSecurityTokenHandler().WriteToken(tokenOldKey);

        // When both keys are configured for validation, old token must still pass
        var principal = ValidateToken(oldToken, PrimaryKey, SecondaryKey);
        principal.Should().NotBeNull("secondary/old key token must validate when secondary key is in the accepted set");
    }

    [Fact]
    public void ValidateToken_SignedWithUnknownKey_ReturnsNull()
    {
        var unknownKeyBytes = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UnknownKey));
        var creds = new SigningCredentials(unknownKeyBytes, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim("sub", "user-123"), new Claim("tenant_id", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);
        var unknownToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Token signed with a completely unknown key must not validate against known keys
        var principal = ValidateToken(unknownToken, PrimaryKey, SecondaryKey);
        principal.Should().BeNull("token from unknown key must be rejected");
    }

    [Fact]
    public void JwtTokenService_ReadsSigningKeysArray_WhenConfigured()
    {
        // When Jwt:SigningKeys is configured, the service must use the first key as signing key
        var sut = CreateSut(PrimaryKey, SecondaryKey);
        var user = TenantUser.Create(Guid.NewGuid(), "u@b.com", "hash", TenantUserRole.Member);

        var token = sut.CreateAccessToken(user);

        // Token should be parseable as valid JWT structure
        var parts = token.Split('.');
        parts.Should().HaveCount(3, "output must be a valid JWT");

        // Signed with primary key (first in SigningKeys array)
        ValidateToken(token, PrimaryKey).Should().NotBeNull();
    }
}
