using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Chronith.Tests.Functional.Fixtures;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Tests.Functional.Security;

/// <summary>
/// Functional tests for JWT validation on protected endpoints.
/// Covers: valid token accepted, expired token rejected, wrong-key token rejected.
/// </summary>
[Collection("Functional")]
public sealed class MultiKeyJwtTests(FunctionalTestFixture fixture)
{
    private const string ProtectedEndpoint = "/v1/booking-types";

    [Fact]
    public async Task ValidJwt_SignedWithTestKey_IsAccepted()
    {
        // Arrange — token signed with the known test signing key
        var client = fixture.CreateClient("TenantAdmin");

        // Act
        var response = await client.GetAsync(ProtectedEndpoint);

        // Assert — server must accept the valid token
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExpiredJwt_IsRejected_With401()
    {
        // Arrange — build a token that is already expired
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiredToken = new JwtSecurityToken(
            claims:
            [
                new Claim("tenant_id", TestConstants.TenantId.ToString()),
                new Claim("sub", TestConstants.AdminUserId),
                new Claim("role", "TenantAdmin"),
            ],
            // notBefore in the past, expires already in the past
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync(ProtectedEndpoint);

        // Assert — expired token must be rejected
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtSignedWithWrongKey_IsRejected_With401()
    {
        // Arrange — sign a token with a completely different key
        var wrongKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("wrong-key-never-registered-in-server-32ch!!"));
        var creds = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256);

        var badToken = new JwtSecurityToken(
            claims:
            [
                new Claim("tenant_id", TestConstants.TenantId.ToString()),
                new Claim("sub", TestConstants.AdminUserId),
                new Claim("role", "TenantAdmin"),
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(badToken);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenString);

        // Act
        var response = await client.GetAsync(ProtectedEndpoint);

        // Assert — unknown key must be rejected
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NoToken_OnProtectedEndpoint_Returns401()
    {
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
