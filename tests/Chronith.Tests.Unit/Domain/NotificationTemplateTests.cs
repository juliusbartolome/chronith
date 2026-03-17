using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class NotificationTemplateTests
{
    [Fact]
    public void Create_ReturnsValidInstance()
    {
        var tenantId = Guid.NewGuid();

        var template = NotificationTemplate.Create(
            tenantId: tenantId,
            eventType: "booking.confirmed",
            channelType: "Email",
            subject: "Your booking is confirmed",
            body: "Hello, your booking has been confirmed.");

        template.Id.Should().NotBeEmpty();
        template.TenantId.Should().Be(tenantId);
        template.EventType.Should().Be("booking.confirmed");
        template.ChannelType.Should().Be("Email");
        template.Subject.Should().Be("Your booking is confirmed");
        template.Body.Should().Be("Hello, your booking has been confirmed.");
    }

    [Fact]
    public void Create_IsActiveDefaultsToTrue()
    {
        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.cancelled",
            channelType: "Sms",
            subject: null,
            body: "Your booking was cancelled.");

        template.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_SetsCreatedAtAndUpdatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.confirmed",
            channelType: "Push",
            subject: null,
            body: "Push notification body.");

        var after = DateTimeOffset.UtcNow;

        template.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        template.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateContent_UpdatesSubjectBodyAndUpdatedAt()
    {
        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.confirmed",
            channelType: "Email",
            subject: "Old Subject",
            body: "Old body.");

        var originalCreatedAt = template.CreatedAt;
        var before = DateTimeOffset.UtcNow;

        template.UpdateContent("New Subject", "New body.");

        var after = DateTimeOffset.UtcNow;

        template.Subject.Should().Be("New Subject");
        template.Body.Should().Be("New body.");
        template.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        template.CreatedAt.Should().Be(originalCreatedAt);
    }

    [Fact]
    public void UpdateContent_WithNullSubject_ClearsSubject()
    {
        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.confirmed",
            channelType: "Email",
            subject: "Original Subject",
            body: "Original body.");

        template.UpdateContent(null, "New body.");

        template.Subject.Should().BeNull();
        template.Body.Should().Be("New body.");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.confirmed",
            channelType: "Email",
            subject: null,
            body: "Body.");

        template.Deactivate();

        template.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var template = NotificationTemplate.Create(
            tenantId: Guid.NewGuid(),
            eventType: "booking.confirmed",
            channelType: "Email",
            subject: null,
            body: "Body.");

        template.Deactivate();
        template.Activate();

        template.IsActive.Should().BeTrue();
    }
}
