using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public class WebhookOutboxHandlerDualWriteTests
{
    private readonly IWebhookRepository _webhookRepo = Substitute.For<IWebhookRepository>();
    private readonly IWebhookOutboxRepository _outboxRepo = Substitute.For<IWebhookOutboxRepository>();
    private readonly IBookingTypeRepository _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
    private readonly IBookingUrlSigner _signer = Substitute.For<IBookingUrlSigner>();
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly IOptions<PaymentPageOptions> _pageOptions = Options.Create(new PaymentPageOptions());

    private WebhookOutboxHandler CreateHandler() =>
        new(_webhookRepo, _outboxRepo, _bookingTypeRepo, _signer, _tenantRepo, _pageOptions);

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BookingTypeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private BookingStatusChangedNotification MakeNotification(BookingStatus toStatus) =>
        new(
            BookingId: Guid.NewGuid(),
            TenantId: TenantId,
            BookingTypeId: BookingTypeId,
            BookingTypeSlug: "test-type",
            FromStatus: null,
            ToStatus: toStatus,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "cust-1",
            CustomerEmail: "cust@example.com");

    private static BookingType CreateBookingTypeWithCallback(string url)
    {
        var bt = (BookingType)typeof(TimeSlotBookingType)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, [], null)!
            .Invoke([]);
        typeof(BookingType).GetProperty("Id")!.SetValue(bt, BookingTypeId);
        bt.SetCustomerCallback(url);
        return bt;
    }

    private static BookingType CreateBookingTypeWithoutCallback()
    {
        var bt = (BookingType)typeof(TimeSlotBookingType)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, [], null)!
            .Invoke([]);
        typeof(BookingType).GetProperty("Id")!.SetValue(bt, BookingTypeId);
        return bt;
    }

    [Fact]
    public async Task Handle_Confirmed_WritesBothTenantWebhookAndCustomerCallbackEntries()
    {
        // Arrange
        var notification = MakeNotification(BookingStatus.Confirmed);
        _webhookRepo.ListAsync(TenantId, BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(new List<Webhook> { Webhook.Create(TenantId, BookingTypeId, "https://tenant.example.com/webhook", "secret", WebhookEventTypes.All) });

        var bt = CreateBookingTypeWithCallback("https://customer.example.com/callback");
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bt);

        List<WebhookOutboxEntry> writtenEntries = [];
        _outboxRepo.AddRangeAsync(Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => writtenEntries.AddRange(e)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert — two entries written in one call
        writtenEntries.Should().HaveCount(2);
        writtenEntries.Should().Contain(e => e.Category == OutboxCategory.TenantWebhook && e.WebhookId != null);
        writtenEntries.Should().Contain(e => e.Category == OutboxCategory.CustomerCallback && e.BookingTypeId == BookingTypeId);
    }

    [Fact]
    public async Task Handle_Confirmed_CustomerCallbackEventType_IsCorrect()
    {
        var notification = MakeNotification(BookingStatus.Confirmed);
        _webhookRepo.ListAsync(TenantId, BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(new List<Webhook>());

        var bt = CreateBookingTypeWithCallback("https://customer.example.com/callback");
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bt);

        List<WebhookOutboxEntry> writtenEntries = [];
        _outboxRepo.AddRangeAsync(Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => writtenEntries.AddRange(e)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(notification, CancellationToken.None);

        var callbackEntry = writtenEntries.Single(e => e.Category == OutboxCategory.CustomerCallback);
        callbackEntry.EventType.Should().Be("customer.booking.confirmed");
    }

    [Fact]
    public async Task Handle_WhenNoCustomerCallbackUrl_WritesOnlyTenantWebhookEntries()
    {
        var notification = MakeNotification(BookingStatus.Confirmed);
        _webhookRepo.ListAsync(TenantId, BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(new List<Webhook> { Webhook.Create(TenantId, BookingTypeId, "https://tenant.example.com/webhook", "secret", WebhookEventTypes.All) });

        // BookingType has no callback URL
        var bt = CreateBookingTypeWithoutCallback();
        _bookingTypeRepo.GetByIdAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bt);

        List<WebhookOutboxEntry> writtenEntries = [];
        _outboxRepo.AddRangeAsync(Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => writtenEntries.AddRange(e)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        await handler.Handle(notification, CancellationToken.None);

        writtenEntries.Should().HaveCount(1);
        writtenEntries.Single().Category.Should().Be(OutboxCategory.TenantWebhook);
    }
}
