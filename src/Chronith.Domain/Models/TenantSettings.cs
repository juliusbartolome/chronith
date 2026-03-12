namespace Chronith.Domain.Models;

public sealed class TenantSettings
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string? LogoUrl { get; private set; }
    public string PrimaryColor { get; private set; } = "#2563EB";
    public string? AccentColor { get; private set; }
    public string? CustomDomain { get; private set; }
    public bool BookingPageEnabled { get; private set; } = true;
    public string? WelcomeMessage { get; private set; }
    public string? TermsUrl { get; private set; }
    public string? PrivacyUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TenantSettings Create(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        return new TenantSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingPageEnabled = true,
            PrimaryColor = "#2563EB",
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    internal TenantSettings() { } // EF Core hydration

    public void UpdateBranding(string? logoUrl, string? primaryColor, string? accentColor,
        string? welcomeMessage, string? termsUrl, string? privacyUrl)
    {
        LogoUrl = logoUrl;
        if (primaryColor is not null) PrimaryColor = primaryColor;
        AccentColor = accentColor;
        WelcomeMessage = welcomeMessage;
        TermsUrl = termsUrl;
        PrivacyUrl = privacyUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCustomDomain(string? customDomain)
    {
        CustomDomain = customDomain;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnableBookingPage()
    {
        BookingPageEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DisableBookingPage()
    {
        BookingPageEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
