using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Chronith.Application.Services;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Chronith.Infrastructure.Auth;

public sealed class OidcTokenValidator : IOidcTokenValidator
{
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>
        ConfigManagers = new();

    public async Task<OidcValidationResult> ValidateAsync(
        string idToken, string issuer, string clientId, string? audience,
        CancellationToken ct = default)
    {
        try
        {
            var configManager = ConfigManagers.GetOrAdd(issuer, key =>
            {
                var metadataAddress = key.TrimEnd('/') + "/.well-known/openid-configuration";
                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever());
            });

            var config = await configManager.GetConfigurationAsync(ct);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = issuer,
                ValidAudience = audience ?? clientId,
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(idToken, validationParameters, out _);

            var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
            var name = principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue("name");

            if (string.IsNullOrWhiteSpace(sub))
                return new OidcValidationResult(false, null, null, null, "Token missing 'sub' claim.");

            return new OidcValidationResult(true, sub, email, name, null);
        }
        catch (SecurityTokenException ex)
        {
            return new OidcValidationResult(false, null, null, null, ex.Message);
        }
        catch (Exception ex)
        {
            return new OidcValidationResult(false, null, null, null, $"OIDC validation failed: {ex.Message}");
        }
    }
}
