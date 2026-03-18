using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantPaymentConfigTests
{
    [Fact]
    public void Create_ApiType_SetsAllPropertiesAndIsActiveFalse()
    {
        var tenantId = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "MyLabel", """{"SecretKey":"sk_test_abc"}""", null, null);

        config.Id.Should().NotBeEmpty();
        config.TenantId.Should().Be(tenantId);
        config.ProviderName.Should().Be("PayMongo");
        config.Label.Should().Be("MyLabel");
        config.IsActive.Should().BeFalse();
        config.IsDeleted.Should().BeFalse();
        config.Settings.Should().Contain("sk_test_abc");
        config.PublicNote.Should().BeNull();
        config.QrCodeUrl.Should().BeNull();
        config.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_ManualType_SetsIsActiveTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "Manual", "GCash", "{}", "Scan to pay via GCash", "https://qr.example.com/gcash");

        config.IsActive.Should().BeTrue();
        config.PublicNote.Should().Be("Scan to pay via GCash");
        config.QrCodeUrl.Should().Be("https://qr.example.com/gcash");
    }

    [Fact]
    public void UpdateDetails_ChangesFieldsLeavesIsActiveUnchanged()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "OldLabel", """{"SecretKey":"sk_old"}""", null, null);
        var originalUpdated = config.UpdatedAt;

        config.UpdateDetails("NewLabel", """{"SecretKey":"sk_new"}""", "note", "https://qr.example.com");

        config.Label.Should().Be("NewLabel");
        config.Settings.Should().Contain("sk_new");
        config.PublicNote.Should().Be("note");
        config.QrCodeUrl.Should().Be("https://qr.example.com");
        config.IsActive.Should().BeFalse(); // unchanged
        config.UpdatedAt.Should().BeOnOrAfter(originalUpdated);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        config.IsActive.Should().BeFalse();

        config.Activate();

        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "Manual", "Cash", "{}", null, null);
        config.IsActive.Should().BeTrue();

        config.Deactivate();

        config.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        config.IsDeleted.Should().BeFalse();

        config.SoftDelete();

        config.IsDeleted.Should().BeTrue();
    }
}
