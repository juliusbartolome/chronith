using System.Diagnostics.Metrics;
using System.Text.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Notifications;
using Chronith.Infrastructure.Services;
using Chronith.Infrastructure.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class NotificationDispatcherTemplateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ChronithMetrics CreateMetrics()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var sp = services.BuildServiceProvider();
        return new ChronithMetrics(sp.GetRequiredService<IMeterFactory>());
    }

    private (
        NotificationDispatcherService Service,
        IWebhookOutboxRepository OutboxRepo,
        INotificationConfigRepository ConfigRepo,
        INotificationTemplateRepository TemplateRepo,
        ITemplateRenderer TemplateRenderer,
        INotificationChannel Channel)
        BuildSut(IEnumerable<PendingOutboxEntry>? pending = null)
    {
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var configRepo = Substitute.For<INotificationConfigRepository>();
        var templateRepo = Substitute.For<INotificationTemplateRepository>();
        var templateRenderer = Substitute.For<ITemplateRenderer>();
        var channel = Substitute.For<INotificationChannel>();

        channel.ChannelType.Returns("email");

        var config = TenantNotificationConfig.Create(TenantId, "email", "{}");

        outboxRepo.GetPendingByCategoryAsync(Arg.Any<OutboxCategory>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((pending ?? []).ToList().AsReadOnly() as IReadOnlyList<PendingOutboxEntry>
                     ?? new List<PendingOutboxEntry>().AsReadOnly());

        configRepo.GetByChannelTypeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(config);

        // Default: renderer just echoes the template body
        templateRenderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(ci => ci.Arg<string>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IWebhookOutboxRepository)).Returns(outboxRepo);
        serviceProvider.GetService(typeof(INotificationConfigRepository)).Returns(configRepo);
        serviceProvider.GetService(typeof(INotificationTemplateRepository)).Returns(templateRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var channelFactory = new NotificationChannelFactory([channel]);

        var options = Options.Create(new NotificationDispatcherOptions { DispatchIntervalSeconds = 1 });
        var logger = Substitute.For<ILogger<NotificationDispatcherService>>();
        var healthTracker = Substitute.For<IBackgroundServiceHealthTracker>();

        var sut = new NotificationDispatcherService(
            scopeFactory, channelFactory, templateRenderer, options, healthTracker, CreateMetrics(), logger);

        return (sut, outboxRepo, configRepo, templateRepo, templateRenderer, channel);
    }

    private static PendingOutboxEntry CreateEmailEntry(
        Guid? id = null,
        string eventType = "notification.booking_confirmed.email",
        string bookingEventType = "booking.confirmed",
        string status = "confirmed")
    {
        var payload = JsonSerializer.Serialize(new
        {
            customerEmail = "customer@example.com",
            @event = bookingEventType,
            status,
            bookingTypeSlug = "massage-therapy"
        });

        return new PendingOutboxEntry(
            id ?? Guid.NewGuid(),
            TenantId,
            null,
            null,
            eventType,
            payload,
            0,
            OutboxCategory.Notification);
    }

    [Fact]
    public async Task DispatchBatchAsync_WithTemplate_CallsGetByEventAndChannelAsync()
    {
        var entry = CreateEmailEntry();
        var template = NotificationTemplate.Create(TenantId, "booking.confirmed", "email",
            "Booking Confirmed", "Hi {{customer_name}}, your booking is confirmed.");

        var (sut, _, _, templateRepo, _, _) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(TenantId, "booking.confirmed", "email", Arg.Any<CancellationToken>())
            .Returns(template);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await templateRepo.Received(1).GetByEventAndChannelAsync(
            TenantId,
            "booking.confirmed",
            "email",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WithTemplate_CallsTemplateRenderer()
    {
        var entry = CreateEmailEntry();
        var template = NotificationTemplate.Create(TenantId, "booking.confirmed", "email",
            "Booking Confirmed", "Hi {{customer_name}}, your booking is confirmed.");

        var (sut, _, _, templateRepo, templateRenderer, _) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(template);

        await sut.DispatchBatchAsync(CancellationToken.None);

        templateRenderer.Received(1).Render(
            template.Body,
            Arg.Any<IReadOnlyDictionary<string, string>>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WithTemplate_UsesRenderedBodyInMessage()
    {
        var entry = CreateEmailEntry();
        var template = NotificationTemplate.Create(TenantId, "booking.confirmed", "email",
            "Booking Confirmed", "Hi {{customer_name}}, your booking is confirmed.");

        var (sut, _, _, templateRepo, templateRenderer, channel) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(template);
        templateRenderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns("Hi Alice, your booking is confirmed.");

        await sut.DispatchBatchAsync(CancellationToken.None);

        await channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Body == "Hi Alice, your booking is confirmed."),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WithTemplate_UsesSubjectFromTemplate()
    {
        var entry = CreateEmailEntry();
        var template = NotificationTemplate.Create(TenantId, "booking.confirmed", "email",
            "Booking Confirmed — massage-therapy", "Body content here.");

        var (sut, _, _, templateRepo, _, channel) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(template);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Subject == "Booking Confirmed — massage-therapy"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_NoTemplate_UsesFallbackBody()
    {
        var entry = CreateEmailEntry();

        var (sut, _, _, templateRepo, templateRenderer, channel) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(default(NotificationTemplate));

        await sut.DispatchBatchAsync(CancellationToken.None);

        // When no template, renderer should not be called
        templateRenderer.DidNotReceive().Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>());

        // But channel should still be called with a fallback message
        await channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Body.Length > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WithTemplate_ContextContainsBookingTypeSlug()
    {
        var entry = CreateEmailEntry();
        var template = NotificationTemplate.Create(TenantId, "booking.confirmed", "email",
            "Subject", "Your booking for {{booking_type_slug}} is confirmed.");

        IReadOnlyDictionary<string, string>? capturedContext = null;
        var (sut, _, _, templateRepo, templateRenderer, _) = BuildSut([entry]);
        templateRepo.GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(template);
        templateRenderer.Render(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(ci =>
            {
                capturedContext = ci.Arg<IReadOnlyDictionary<string, string>>();
                return "rendered";
            });

        await sut.DispatchBatchAsync(CancellationToken.None);

        capturedContext.Should().NotBeNull();
        capturedContext!.Should().ContainKey("booking_type_slug");
        capturedContext["booking_type_slug"].Should().Be("massage-therapy");
        capturedContext.Should().ContainKey("customer_name");
        capturedContext.Should().ContainKey("customer_email");
        capturedContext["customer_email"].Should().Be("customer@example.com");
        capturedContext.Should().ContainKey("status");
        capturedContext["status"].Should().Be("confirmed");
        capturedContext.Should().ContainKey("event_type");
        capturedContext["event_type"].Should().Be("booking.confirmed");
    }
}
