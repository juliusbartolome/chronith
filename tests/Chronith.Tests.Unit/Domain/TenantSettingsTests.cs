using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantSettingsTests
{
    [Fact]
    public void Create_WithValidData_ReturnsTenantSettings()
    {
        var tenantId = Guid.NewGuid();

        var settings = TenantSettings.Create(tenantId);

        settings.Id.Should().NotBeEmpty();
        settings.TenantId.Should().Be(tenantId);
        settings.BookingPageEnabled.Should().BeTrue();
        settings.PrimaryColor.Should().Be("#2563EB");
        settings.CreatedAt.Should().NotBe(default);
        settings.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public void UpdateBranding_SetsBrandingFields()
    {
        var settings = TenantSettings.Create(Guid.NewGuid());

        settings.UpdateBranding(
            logoUrl: "https://example.com/logo.png",
            primaryColor: "#FF5733",
            accentColor: "#33FF57",
            welcomeMessage: "Welcome to our booking portal!",
            termsUrl: "https://example.com/terms",
            privacyUrl: "https://example.com/privacy");

        settings.LogoUrl.Should().Be("https://example.com/logo.png");
        settings.PrimaryColor.Should().Be("#FF5733");
        settings.AccentColor.Should().Be("#33FF57");
        settings.WelcomeMessage.Should().Be("Welcome to our booking portal!");
        settings.TermsUrl.Should().Be("https://example.com/terms");
        settings.PrivacyUrl.Should().Be("https://example.com/privacy");
    }

    [Fact]
    public void SetCustomDomain_SetsCustomDomain()
    {
        var settings = TenantSettings.Create(Guid.NewGuid());

        settings.SetCustomDomain("booking.example.com");

        settings.CustomDomain.Should().Be("booking.example.com");
    }

    [Fact]
    public void DisableBookingPage_SetsBookingPageEnabledFalse()
    {
        var settings = TenantSettings.Create(Guid.NewGuid());
        settings.BookingPageEnabled.Should().BeTrue();

        settings.DisableBookingPage();

        settings.BookingPageEnabled.Should().BeFalse();
    }

    [Fact]
    public void EnableBookingPage_SetsBookingPageEnabledTrue()
    {
        var settings = TenantSettings.Create(Guid.NewGuid());
        settings.DisableBookingPage();
        settings.BookingPageEnabled.Should().BeFalse();

        settings.EnableBookingPage();

        settings.BookingPageEnabled.Should().BeTrue();
    }

    [Fact]
    public void UpdateBranding_WithNullFields_DoesNotClearExistingValues()
    {
        var settings = TenantSettings.Create(Guid.NewGuid());
        settings.UpdateBranding("https://example.com/logo.png", null, null, null, null, null);

        settings.UpdateBranding(null, "#FF0000", null, null, null, null);

        settings.LogoUrl.Should().Be("https://example.com/logo.png"); // not cleared
        settings.PrimaryColor.Should().Be("#FF0000");
    }
}
