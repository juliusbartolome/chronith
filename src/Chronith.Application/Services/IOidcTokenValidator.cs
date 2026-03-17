namespace Chronith.Application.Services;

public interface IOidcTokenValidator
{
    Task<OidcValidationResult> ValidateAsync(
        string idToken, string issuer, string clientId, string? audience,
        CancellationToken ct = default);
}

public sealed record OidcValidationResult(
    bool IsValid, string? ExternalId, string? Email, string? Name, string? Error);
