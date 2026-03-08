using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantNotificationConfigTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var config = TenantNotificationConfig.Create(tenantId, "email", """{"host":"smtp.example.com"}""");

        config.Id.Should().NotBeEmpty();
        config.TenantId.Should().Be(tenantId);
        config.ChannelType.Should().Be("email");
        config.IsEnabled.Should().BeTrue();
        config.Settings.Should().Contain("smtp.example.com");
        config.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateSettings_ChangesSettingsAndUpdatedAt()
    {
        var config = TenantNotificationConfig.Create(Guid.NewGuid(), "sms", """{"from":"+1234"}""");
        var originalUpdated = config.UpdatedAt;

        config.UpdateSettings("""{"from":"+5678"}""");

        config.Settings.Should().Contain("+5678");
        config.UpdatedAt.Should().BeOnOrAfter(originalUpdated);
    }

    [Fact]
    public void Disable_SetsIsEnabledToFalse()
    {
        var config = TenantNotificationConfig.Create(Guid.NewGuid(), "push", "{}");
        config.IsEnabled.Should().BeTrue();

        config.Disable();

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_SetsIsEnabledToTrue()
    {
        var config = TenantNotificationConfig.Create(Guid.NewGuid(), "email", "{}");
        config.Disable();
        config.IsEnabled.Should().BeFalse();

        config.Enable();

        config.IsEnabled.Should().BeTrue();
    }
}
