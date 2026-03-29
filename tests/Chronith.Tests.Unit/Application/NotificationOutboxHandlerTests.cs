using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class NotificationOutboxHandlerTests
{
    private static BookingStatusChangedNotification MakeNotification(
        Guid tenantId, BookingStatus toStatus, Guid? bookingId = null) =>
        new(
            BookingId: bookingId ?? Guid.NewGuid(),
            TenantId: tenantId,
            BookingTypeId: Guid.NewGuid(),
            BookingTypeSlug: "test-type",
            FromStatus: BookingStatus.PendingPayment,
            ToStatus: toStatus,
            Start: DateTimeOffset.UtcNow,
            End: DateTimeOffset.UtcNow.AddHours(1),
            CustomerId: "cust-1",
            CustomerEmail: "cust@example.com",
            CustomerFirstName: "Test",
            CustomerLastName: "User",
            CustomerMobile: "+639170000000");

    [Fact]
    public async Task Handle_PendingVerification_PayloadIncludesStaffVerifyUrl()
    {
        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var configRepo = Substitute.For<INotificationConfigRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
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

        var configs = new List<TenantNotificationConfig>
        {
            TenantNotificationConfig.Create(tenantId, "email", "{}")
        };
        configRepo.ListEnabledByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(configs.AsReadOnly());

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new NotificationOutboxHandler(configRepo, outboxRepo, signer, tenantRepo, options);
        var notification = MakeNotification(tenantId, BookingStatus.PendingVerification, bookingId);

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
        var configRepo = Substitute.For<INotificationConfigRepository>();
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var signer = Substitute.For<IBookingUrlSigner>();
        var tenantRepo = Substitute.For<ITenantRepository>();
        var options = Options.Create(new PaymentPageOptions());

        var configs = new List<TenantNotificationConfig>
        {
            TenantNotificationConfig.Create(tenantId, "email", "{}")
        };
        configRepo.ListEnabledByTenantAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(configs.AsReadOnly());

        var capturedEntries = new List<WebhookOutboxEntry>();
        await outboxRepo.AddRangeAsync(
            Arg.Do<IEnumerable<WebhookOutboxEntry>>(e => capturedEntries.AddRange(e)),
            Arg.Any<CancellationToken>());

        var handler = new NotificationOutboxHandler(configRepo, outboxRepo, signer, tenantRepo, options);
        var notification = MakeNotification(tenantId, BookingStatus.Confirmed);

        await handler.Handle(notification, CancellationToken.None);

        capturedEntries.Should().HaveCount(1);
        var payload = JsonDocument.Parse(capturedEntries[0].Payload);
        payload.RootElement.TryGetProperty("staffVerifyUrl", out var urlProp).Should().BeTrue();
        urlProp.ValueKind.Should().Be(JsonValueKind.Null);
    }
}
