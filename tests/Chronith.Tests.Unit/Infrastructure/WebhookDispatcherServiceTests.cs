using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class WebhookDispatcherServiceTests
{
    private static (
        WebhookDispatcherService Service,
        IWebhookOutboxRepository OutboxRepo,
        IWebhookRepository WebhookRepo,
        FakeHttpMessageHandler HttpHandler)
        BuildSut(IEnumerable<PendingOutboxEntry>? pending = null)
    {
        var outboxRepo = Substitute.For<IWebhookOutboxRepository>();
        var webhookRepo = Substitute.For<IWebhookRepository>();

        outboxRepo.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((pending ?? []).ToList().AsReadOnly() as IReadOnlyList<PendingOutboxEntry>
                     ?? new List<PendingOutboxEntry>().AsReadOnly());

        // Wire up scope factory
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IWebhookOutboxRepository)).Returns(outboxRepo);
        serviceProvider.GetService(typeof(IWebhookRepository)).Returns(webhookRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(httpHandler);
        httpHandler.Client = httpClient;
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("WebhookDispatcher").Returns(httpClient);

        var options = Options.Create(new WebhookDispatcherOptions { DispatchIntervalSeconds = 1, HttpTimeoutSeconds = 5 });
        var logger = Substitute.For<ILogger<WebhookDispatcherService>>();

        var sut = new WebhookDispatcherService(scopeFactory, httpClientFactory, options, logger);

        return (sut, outboxRepo, webhookRepo, httpHandler);
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
        var entry = new PendingOutboxEntry(entryId, webhookId, "booking.confirmed", "{}", AttemptCount: 0);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret");

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
        var entry = new PendingOutboxEntry(entryId, webhookId, "booking.confirmed", "{}", AttemptCount: 0);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret");

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
        var entry = new PendingOutboxEntry(entryId, webhookId, "booking.confirmed", "{}", AttemptCount: 0);

        var (sut, outboxRepo, webhookRepo, _) = BuildSut(pending: [entry]);
        webhookRepo.GetByIdCrossTenantAsync(webhookId, Arg.Any<CancellationToken>()).Returns((Webhook?)null);

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
        var entry = new PendingOutboxEntry(entryId, webhookId, "booking.confirmed", "{}", AttemptCount: 4);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret");

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
        var entry = new PendingOutboxEntry(entryId, webhookId, "booking.confirmed", "{}", AttemptCount: 5);
        var webhook = Webhook.Create(Guid.NewGuid(), Guid.NewGuid(), "https://example.com/hook", "secret");

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

    public HttpClient? Client { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(StatusCode));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client?.Dispose();
            Client = null;
        }

        base.Dispose(disposing);
    }
}
