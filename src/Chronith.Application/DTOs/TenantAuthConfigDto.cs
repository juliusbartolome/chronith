namespace Chronith.Application.DTOs;

public sealed record TenantAuthConfigDto(
    Guid Id,
    bool AllowBuiltInAuth,
    string? OidcIssuer,
    string? OidcClientId,
    string? OidcAudience,
    bool MagicLinkEnabled);
