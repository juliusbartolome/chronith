using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class WebhookTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingTypeId = Guid.NewGuid();
    private const string Url = "https://example.com/webhook";
    private const string Secret = "supersecretvalue1234567890";

    [Fact]
    public void Create_WithValidEventTypes_SetsEventTypes()
    {
        var eventTypes = new List<string>
        {
            WebhookEventTypes.Confirmed,
            WebhookEventTypes.Cancelled
        };

        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret, eventTypes);

        webhook.EventTypes.Should().BeEquivalentTo(eventTypes);
    }

    [Fact]
    public void Create_WithEmptyEventTypes_ThrowsArgumentException()
    {
        var act = () => Webhook.Create(TenantId, BookingTypeId, Url, Secret, []);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("eventTypes");
    }

    [Fact]
    public void Create_WithUnknownEventType_ThrowsArgumentException()
    {
        var eventTypes = new List<string> { "booking.unknown" };

        var act = () => Webhook.Create(TenantId, BookingTypeId, Url, Secret, eventTypes);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("eventTypes")
            .Which.Message.Should().Contain("booking.unknown");
    }

    [Fact]
    public void Create_WithDuplicateEventTypes_Deduplicates()
    {
        var eventTypes = new List<string>
        {
            WebhookEventTypes.Confirmed,
            WebhookEventTypes.Confirmed,
            WebhookEventTypes.Cancelled
        };

        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret, eventTypes);

        webhook.EventTypes.Should().HaveCount(2);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.Confirmed);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.Cancelled);
    }

    [Fact]
    public void UpdateSubscriptions_WithValidEventTypes_ReplacesExisting()
    {
        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret,
            [WebhookEventTypes.Confirmed]);

        webhook.UpdateSubscriptions([WebhookEventTypes.Cancelled, WebhookEventTypes.PaymentReceived]);

        webhook.EventTypes.Should().HaveCount(2);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.Cancelled);
        webhook.EventTypes.Should().Contain(WebhookEventTypes.PaymentReceived);
        webhook.EventTypes.Should().NotContain(WebhookEventTypes.Confirmed);
    }

    [Fact]
    public void UpdateSubscriptions_WithEmpty_ThrowsArgumentException()
    {
        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret,
            [WebhookEventTypes.Confirmed]);

        var act = () => webhook.UpdateSubscriptions([]);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("eventTypes");
    }

    [Fact]
    public void Update_WithEventTypes_ReplacesSubscriptions()
    {
        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret,
            [WebhookEventTypes.Confirmed]);

        webhook.Update(null, null, [WebhookEventTypes.PaymentFailed]);

        webhook.EventTypes.Should().ContainSingle()
            .Which.Should().Be(WebhookEventTypes.PaymentFailed);
        // Url and secret unchanged
        webhook.Url.Should().Be(Url);
        webhook.Secret.Should().Be(Secret);
    }

    [Fact]
    public void Update_WithNullEventTypes_PreservesExisting()
    {
        var webhook = Webhook.Create(TenantId, BookingTypeId, Url, Secret,
            [WebhookEventTypes.Confirmed]);

        webhook.Update("https://new.example.com/hook", null, null);

        webhook.EventTypes.Should().ContainSingle()
            .Which.Should().Be(WebhookEventTypes.Confirmed);
        webhook.Url.Should().Be("https://new.example.com/hook");
    }
}
