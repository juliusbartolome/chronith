using System.Diagnostics.Metrics;
using System.Net;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services;
using Chronith.Infrastructure.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class WebhookDispatcherServiceTests : IDisposable
{
    // HttpClient is kept alive for the test class lifetime and disposed in Dispose().
    private HttpClient? _httpClient;

    public void Dispose() => _httpClient?.Dispose();

    private (
        WebhookDispatcherService Service,
        IWebhookOutboxRepository OutboxRepo,
        IWebhookRepository WebhookRepo,
        FakeHttpMessageHandler HttpHandler)
        BuildSut(IEnumerable<PendingOutboxEntry>? pending = null)
    {
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var webhookRepo = Substitute.For<IWebhookRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();

        outboxRepo.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((pending ?? []).ToList().AsReadOnly() as IReadOnlyList<PendingOutboxEntry>
                     ?? new List<PendingOutboxEntry>().AsReadOnly());

        // Wire up scope factory
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IWebhookOutboxRepository)).Returns(outboxRepo);
        serviceProvider.GetService(typeof(IWebhookRepository)).Returns(webhookRepo);
        serviceProvider.GetService(typeof(IBookingTypeRepository)).Returns(bookingTypeRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        _httpClient = new HttpClient(httpHandler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("WebhookDispatcher").Returns(_httpClient);

        var options = Options.Create(new WebhookDispatcherOptions { DispatchIntervalSeconds = 1, HttpTimeoutSeconds = 5 });
        var logger = Substitute.For<ILogger<WebhookDispatcherService>>();
        var healthTracker = Substitute.For<IBackgroundServiceHealthTracker>();

        var sut = new WebhookDispatcherService(scopeFactory, httpClientFactory, options, healthTracker, CreateMetrics(), logger);

        return (sut, outboxRepo, webhookRepo, httpHandler);
    }

    private static ChronithMetrics CreateMetrics()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var sp = services.BuildServiceProvider();
        return new ChronithMetrics(sp.GetRequiredService<IMeterFactory>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WithNoPendingEntries_DoesNotCallWebhookRepo()
    {
        var (sut, _, webhookRepo, _) = BuildSut(pending: []);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await webhookRepo.DidNotReceive().GetByIdCrossTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_SuccessfulDelivery_CallsMarkDelivered()
    {
        var entryId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var entry = new PendingOutboxEntry(entryId, Guid.NewGuid(), webhookId, null, "booking.confirmed", "{}", 0, OutboxCategory.TenantWebhook);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret", WebhookEventTypes.All);

        var (sut, outboxRepo, webhookRepo, httpHandler) = BuildSut(pending: [entry]);
        httpHandler.StatusCode = HttpStatusCode.OK;
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns(webhook);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await outboxRepo.Received(1).MarkDeliveredAsync(entryId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await outboxRepo.DidNotReceive().MarkFailedAttemptAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_FailedDelivery_CallsMarkFailedAttempt()
    {
        var entryId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var entry = new PendingOutboxEntry(entryId, Guid.NewGuid(), webhookId, null, "booking.confirmed", "{}", 0, OutboxCategory.TenantWebhook);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret", WebhookEventTypes.All);

        var (sut, outboxRepo, webhookRepo, httpHandler) = BuildSut(pending: [entry]);
        httpHandler.StatusCode = HttpStatusCode.InternalServerError;
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns(webhook);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await outboxRepo.Received(1).MarkFailedAttemptAsync(
            entryId, 1, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset?>(), false, Arg.Any<CancellationToken>());
        await outboxRepo.DidNotReceive().MarkDeliveredAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_WebhookNotFound_PermanentlyFailsEntry()
    {
        var entryId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var entry = new PendingOutboxEntry(entryId, Guid.NewGuid(), webhookId, null, "booking.confirmed", "{}", 0, OutboxCategory.TenantWebhook);

        var (sut, outboxRepo, webhookRepo, _) = BuildSut(pending: [entry]);
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns(default(Webhook));

        await sut.DispatchBatchAsync(CancellationToken.None);

        await outboxRepo.DidNotReceive().MarkDeliveredAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await outboxRepo.Received(1).MarkFailedAttemptAsync(
            entryId,
            WebhookOutboxEntry.MaxAttempts,
            Arg.Any<DateTimeOffset>(),
            null,
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_OnAttempt5_SetsNextRetryAt_AndNotFinal()
    {
        // Attempt 5 → newAttemptCount = 5 → isFinal = false (MaxAttempts = 6)
        // GetBackOffDelay(5) = 4 hours
        var entryId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var entry = new PendingOutboxEntry(entryId, Guid.NewGuid(), webhookId, null, "booking.confirmed", "{}", 4, OutboxCategory.TenantWebhook);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret", WebhookEventTypes.All);

        var (sut, outboxRepo, webhookRepo, httpHandler) = BuildSut(pending: [entry]);
        httpHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns(webhook);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await outboxRepo.Received(1).MarkFailedAttemptAsync(
            entryId,
            5,
            Arg.Any<DateTimeOffset>(),
            Arg.Is<DateTimeOffset?>(d => d.HasValue),  // nextRetryAt should be set
            false,  // not final
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchBatchAsync_OnAttempt6_MarksAsFinal()
    {
        // AttemptCount = 5 → newAttemptCount = 6 → isFinal = true (MaxAttempts = 6)
        var entryId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        var entry = new PendingOutboxEntry(entryId, Guid.NewGuid(), webhookId, null, "booking.confirmed", "{}", 5, OutboxCategory.TenantWebhook);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret", WebhookEventTypes.All);

        var (sut, outboxRepo, webhookRepo, httpHandler) = BuildSut(pending: [entry]);
        httpHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns(webhook);

        await sut.DispatchBatchAsync(CancellationToken.None);

        await outboxRepo.Received(1).MarkFailedAttemptAsync(
            entryId,
            6,
            Arg.Any<DateTimeOffset>(),
            null,   // nextRetryAt = null when final
            true,   // isFinal = true
            Arg.Any<CancellationToken>());
    }
}

/// <summary>
/// Simple fake HTTP handler that returns a configurable status code.
/// </summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
{
    public HttpStatusCode StatusCode { get; set; } = statusCode;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Ownership of HttpResponseMessage transfers to HttpClient, which disposes it.
        var response = new HttpResponseMessage(StatusCode); // lgtm[cs/local-not-disposed] // codeql[cs/local-not-disposed]
        return Task.FromResult(response);
    }
}
