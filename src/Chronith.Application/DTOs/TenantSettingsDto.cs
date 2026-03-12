namespace Chronith.Application.DTOs;

public sealed record TenantSettingsDto(
    Guid Id,
    Guid TenantId,
    string? LogoUrl,
    string PrimaryColor,
    string? AccentColor,
    string? CustomDomain,
    bool BookingPageEnabled,
    string? WelcomeMessage,
    string? TermsUrl,
    string? PrivacyUrl,
    DateTimeOffset UpdatedAt);
