using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantSettingsEntityMapper
{
    public static TenantSettings ToDomain(TenantSettingsEntity entity)
    {
        var domain = new TenantSettings();
        SetProperty(domain, nameof(TenantSettings.Id), entity.Id);
        SetProperty(domain, nameof(TenantSettings.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TenantSettings.LogoUrl), entity.LogoUrl);
        SetProperty(domain, nameof(TenantSettings.PrimaryColor), entity.PrimaryColor);
        SetProperty(domain, nameof(TenantSettings.AccentColor), entity.AccentColor);
        SetProperty(domain, nameof(TenantSettings.CustomDomain), entity.CustomDomain);
        SetProperty(domain, nameof(TenantSettings.BookingPageEnabled), entity.BookingPageEnabled);
        SetProperty(domain, nameof(TenantSettings.WelcomeMessage), entity.WelcomeMessage);
        SetProperty(domain, nameof(TenantSettings.TermsUrl), entity.TermsUrl);
        SetProperty(domain, nameof(TenantSettings.PrivacyUrl), entity.PrivacyUrl);
        SetProperty(domain, nameof(TenantSettings.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(TenantSettings.UpdatedAt), entity.UpdatedAt);
        return domain;
    }

    public static TenantSettingsEntity ToEntity(TenantSettings domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            LogoUrl = domain.LogoUrl,
            PrimaryColor = domain.PrimaryColor,
            AccentColor = domain.AccentColor,
            CustomDomain = domain.CustomDomain,
            BookingPageEnabled = domain.BookingPageEnabled,
            WelcomeMessage = domain.WelcomeMessage,
            TermsUrl = domain.TermsUrl,
            PrivacyUrl = domain.PrivacyUrl,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        prop?.SetValue(target, value);
    }
}
