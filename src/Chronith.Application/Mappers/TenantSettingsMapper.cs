using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantSettingsMapper
{
    public static TenantSettingsDto ToDto(this TenantSettings settings) => new(
        Id: settings.Id,
        TenantId: settings.TenantId,
        LogoUrl: settings.LogoUrl,
        PrimaryColor: settings.PrimaryColor,
        AccentColor: settings.AccentColor,
        CustomDomain: settings.CustomDomain,
        BookingPageEnabled: settings.BookingPageEnabled,
        WelcomeMessage: settings.WelcomeMessage,
        TermsUrl: settings.TermsUrl,
        PrivacyUrl: settings.PrivacyUrl,
        UpdatedAt: settings.UpdatedAt);
}
