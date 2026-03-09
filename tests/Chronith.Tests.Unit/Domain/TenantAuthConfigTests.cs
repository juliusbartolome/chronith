using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantAuthConfigTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var tenantId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var config = TenantAuthConfig.Create(tenantId);

        config.Id.Should().NotBeEmpty();
        config.TenantId.Should().Be(tenantId);
        config.AllowBuiltInAuth.Should().BeTrue();
        config.OidcIssuer.Should().BeNull();
        config.OidcClientId.Should().BeNull();
        config.OidcAudience.Should().BeNull();
        config.MagicLinkEnabled.Should().BeFalse();
        config.CreatedAt.Should().BeOnOrAfter(before);
        config.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Update_ChangesOidcSettings()
    {
        var config = TenantAuthConfig.Create(Guid.NewGuid());
        var originalUpdated = config.UpdatedAt;

        config.Update(
            allowBuiltInAuth: true,
            oidcIssuer: "https://accounts.google.com",
            oidcClientId: "client-123",
            oidcAudience: "audience-456",
            magicLinkEnabled: true);

        config.AllowBuiltInAuth.Should().BeTrue();
        config.OidcIssuer.Should().Be("https://accounts.google.com");
        config.OidcClientId.Should().Be("client-123");
        config.OidcAudience.Should().Be("audience-456");
        config.MagicLinkEnabled.Should().BeTrue();
        config.UpdatedAt.Should().BeOnOrAfter(originalUpdated);
    }

    [Fact]
    public void Update_CanDisableBuiltInAuth()
    {
        var config = TenantAuthConfig.Create(Guid.NewGuid());
        config.AllowBuiltInAuth.Should().BeTrue();

        config.Update(
            allowBuiltInAuth: false,
            oidcIssuer: "https://login.microsoftonline.com/tenant",
            oidcClientId: "ms-client",
            oidcAudience: "ms-audience",
            magicLinkEnabled: false);

        config.AllowBuiltInAuth.Should().BeFalse();
        config.OidcIssuer.Should().Be("https://login.microsoftonline.com/tenant");
    }
}
