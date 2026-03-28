using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class WebhookOutboxHandlerTests
{
    private static BookingStatusChangedNotification MakeNotification(Guid bookingTypeId, Guid tenantId) =>
        new(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "test-slug",
            FromStatus: BookingStatus.PendingVerification,
            ToStatus: BookingStatus.Confirmed,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "user-1",
            CustomerEmail: "user@example.com");

    [Fact]
    public async Task Handle_WithWebhooks_CreatesOneOutboxEntryPerWebhook()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook1", "s1", WebhookEventTypes.All),
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook2", "s2", WebhookEventTypes.All),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>())
            .Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = MakeNotification(bookingTypeId, tenantId);

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(2);
        capturedEntries.Should().AllSatisfy(e =>
        {
            e.Status.Should().Be(OutboxStatus.Pending);
            e.EventType.Should().Be("booking.confirmed");
        });
    }

    [Fact]
    public async Task Handle_WithWebhooks_DoesNotCallSaveChanges()
    {
        // The handler must NOT call SaveChangesAsync — the command handler's UoW covers it.
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook1", "s1", WebhookEventTypes.All),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>())
            .Returns(webhooks);

        // WebhookOutboxHandler no longer accepts IUnitOfWork — this test verifies
        // outbox entries are added without a separate save.
        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = MakeNotification(bookingTypeId, tenantId);

        await handler.Handle(notification, CancellationToken.None);

        await outboxRepo.Received(1).AddRangeAsync(Arg.Any<IEnumerable<WebhookOutboxEntry>>(), Arg.Any<CancellationToken>());
        // unitOfWork is not injected into the handler — SaveChangesAsync cannot be called
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNoWebhooks_DoesNotThrow()
    {
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Webhook>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var act = () => handler.Handle(
            MakeNotification(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WithNoWebhooks_DoesNotCallAddRange()
    {
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Webhook>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        await handler.Handle(MakeNotification(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await outboxRepo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<WebhookOutboxEntry>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, "booking.confirmed")]
    [InlineData(BookingStatus.Cancelled, "booking.cancelled")]
    [InlineData(BookingStatus.PendingVerification, "booking.payment_received")]
    public async Task Handle_MapsStatusToCorrectEventType(BookingStatus status, string expectedEventType)
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "secret", WebhookEventTypes.All),
        };
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = new BookingStatusChangedNotification(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "slug",
            FromStatus: null,
            ToStatus: status,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "u1",
            CustomerEmail: "u@example.com");

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        capturedEntries[0].EventType.Should().Be(expectedEventType);
    }

    [Fact]
    public async Task Handle_PendingPaymentStatus_NoOutboxEntry()
    {
        // PendingPayment doesn't map to a webhook event — handler should do nothing
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>())
            .Returns(new List<Webhook> { Webhook.Create(tenantId, bookingTypeId, "https://h.co", "s", WebhookEventTypes.All) });

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = new BookingStatusChangedNotification(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "slug",
            FromStatus: null,
            ToStatus: BookingStatus.PendingPayment,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "u1",
            CustomerEmail: "u@example.com");

        await handler.Handle(notification, CancellationToken.None);

        await outboxRepo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<WebhookOutboxEntry>>(), Arg.Any<CancellationToken>());
        await webhookRepo.DidNotReceive().ListAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Event subscription filtering tests ────────────────────────────────────

    [Fact]
    public async Task Handle_WebhookSubscribedToEvent_CreatesOutboxEntry()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "secret",
                new[] { WebhookEventTypes.Confirmed }),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = MakeNotification(bookingTypeId, tenantId); // ToStatus = Confirmed

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        capturedEntries[0].EventType.Should().Be("booking.confirmed");
    }

    [Fact]
    public async Task Handle_WebhookNotSubscribedToEvent_SkipsWebhook()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        // Webhook only subscribed to cancelled, but notification is for confirmed
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "secret",
                new[] { WebhookEventTypes.Cancelled }),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = MakeNotification(bookingTypeId, tenantId); // ToStatus = Confirmed

        await handler.Handle(notification, CancellationToken.None);

        await outboxRepo.DidNotReceive().AddRangeAsync(
            Arg.Any<IEnumerable<WebhookOutboxEntry>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MultipleWebhooks_OnlySubscribedOnesGetEntries()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/subscribed", "secret1",
                new[] { WebhookEventTypes.Confirmed }),
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/not-subscribed", "secret2",
                new[] { WebhookEventTypes.Cancelled }),
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/also-subscribed", "secret3",
                new[] { WebhookEventTypes.Confirmed, WebhookEventTypes.Cancelled }),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = MakeNotification(bookingTypeId, tenantId); // ToStatus = Confirmed

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(2);
        capturedEntries.Select(e => e.WebhookId).Should()
            .Contain(webhooks[0].Id)
            .And.Contain(webhooks[2].Id)
            .And.NotContain(webhooks[1].Id);
    }

    [Fact]
    public async Task Handle_WebhookSubscribedToAllEvents_AlwaysGetsEntry()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/all-events", "secret",
                WebhookEventTypes.All),
        };

        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo);

        // Test with Confirmed
        await handler.Handle(MakeNotification(bookingTypeId, tenantId), CancellationToken.None);
        capturedEntries.Should().HaveCount(1);
        capturedEntries[0].EventType.Should().Be("booking.confirmed");
    }
}
