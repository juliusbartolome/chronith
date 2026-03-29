using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class WebhookOutboxHandlerTests
{
    private static readonly IBookingUrlSigner DefaultSigner = Substitute.For<IBookingUrlSigner>();
    private static readonly ITenantRepository DefaultTenantRepo = Substitute.For<ITenantRepository>();
    private static readonly IOptions<PaymentPageOptions> DefaultOptions = Options.Create(new PaymentPageOptions());

    private static WebhookOutboxHandler CreateHandler(
        IWebhookRepository webhookRepo,
        IWebhookOutboxRepository outboxRepo,
        IBookingTypeRepository bookingTypeRepo,
        IBookingUrlSigner? signer = null,
        ITenantRepository? tenantRepo = null,
        IOptions<PaymentPageOptions>? options = null) =>
        new(webhookRepo, outboxRepo, bookingTypeRepo,
            signer ?? DefaultSigner, tenantRepo ?? DefaultTenantRepo, options ?? DefaultOptions);

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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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
        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        await handler.Handle(MakeNotification(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await outboxRepo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<WebhookOutboxEntry>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed, "booking.confirmed")]
    [InlineData(BookingStatus.Cancelled, "booking.cancelled")]
    [InlineData(BookingStatus.PendingVerification, "booking.payment_received")]
    [InlineData(BookingStatus.PaymentFailed, "booking.payment_failed")]
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

    [Fact]
    public async Task Handle_PaymentFailed_CreatesCustomerCallbackWithCorrectEventType()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();

        // No tenant webhooks
        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>())
            .Returns(new List<Webhook>());

        // Booking type with a customer callback URL
        var bookingType = BookingTypeBuilder.BuildTimeSlot(tenantId: tenantId);
        bookingType.SetCustomerCallback("https://example.com/callback");
        bookingTypeRepo.GetByIdAsync(bookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
        var notification = new BookingStatusChangedNotification(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "slug",
            FromStatus: BookingStatus.PendingPayment,
            ToStatus: BookingStatus.PaymentFailed,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "u1",
            CustomerEmail: "u@example.com");

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        capturedEntries[0].EventType.Should().Be("customer.payment.failed");
        capturedEntries[0].Category.Should().Be(OutboxCategory.CustomerCallback);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);
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

        var handler = CreateHandler(webhookRepo, outboxRepo, bookingTypeRepo);

        // Test with Confirmed
        await handler.Handle(MakeNotification(bookingTypeId, tenantId), CancellationToken.None);
        capturedEntries.Should().HaveCount(1);
        capturedEntries[0].EventType.Should().Be("booking.confirmed");
    }

    // ── Staff verify URL tests ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingVerification_PayloadIncludesStaffVerifyUrl()
    {
        var tenantId = Guid.NewGuid();
        var bookingTypeId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var webhooks = new List<Webhook>
        {
            Webhook.Create(tenantId, bookingTypeId, "https://example.com/hook", "secret", WebhookEventTypes.All),
        };
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        var signer = Substitute.For<IBookingUrlSigner>();
        var tenantRepo = Substitute.For<ITenantRepository>();

        var tenant = Tenant.Create("test-tenant", "Test Tenant", "Asia/Manila");
        tenantRepo.GetByIdAsync(tenantId, Arg.Any<CancellationToken>()).Returns(tenant);

        var options = Options.Create(new PaymentPageOptions
        {
            StaffVerifyBaseUrl = "https://app.example.com/verify"
        });

        signer.GenerateStaffVerifyUrl("https://app.example.com/verify", bookingId, "test-tenant")
            .Returns("https://app.example.com/verify?bookingId=xxx&sig=yyy");

        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo, signer, tenantRepo, options);
        var notification = new BookingStatusChangedNotification(
            BookingId: bookingId,
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "slug",
            FromStatus: BookingStatus.PendingPayment,
            ToStatus: BookingStatus.PendingVerification,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "u1",
            CustomerEmail: "u@example.com");

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        var payload = JsonDocument.Parse(capturedEntries[0].Payload);
        payload.RootElement.GetProperty("staffVerifyUrl").GetString()
            .Should().Be("https://app.example.com/verify?bookingId=xxx&sig=yyy");
    }

    [Fact]
    public async Task Handle_ConfirmedStatus_PayloadDoesNotIncludeStaffVerifyUrl()
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
        var signer = Substitute.For<IBookingUrlSigner>();
        var tenantRepo = Substitute.For<ITenantRepository>();
        var options = Options.Create(new PaymentPageOptions());

        webhookRepo.ListAsync(tenantId, bookingTypeId, Arg.Any<CancellationToken>()).Returns(webhooks);

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new WebhookOutboxHandler(webhookRepo, outboxRepo, bookingTypeRepo, signer, tenantRepo, options);
        var notification = new BookingStatusChangedNotification(
            BookingId: Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: bookingTypeId,
            BookingTypeSlug: "slug",
            FromStatus: BookingStatus.PendingVerification,
            ToStatus: BookingStatus.Confirmed,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "u1",
            CustomerEmail: "u@example.com");

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        var payload = JsonDocument.Parse(capturedEntries[0].Payload);
        payload.RootElement.TryGetProperty("staffVerifyUrl", out var urlProp).Should().BeTrue();
        urlProp.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
