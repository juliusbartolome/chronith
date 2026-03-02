using System.Security.Cryptography;
using System.Text;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Services;

public sealed class WebhookDispatcherService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookDispatcherOptions> options,
    ILogger<WebhookDispatcherService> logger)
    : BackgroundService
{
    // IMPORTANT: MaxAttempts = 6 (after 6 attempts, mark Failed)
    // BackOffSchedule applies to attempts 1-5, attempt 6 is the final failure
    private static readonly TimeSpan[] BackOffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4)
    ];

    private const int MaxAttempts = 6;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during webhook dispatch batch");
            }
            await Task.Delay(TimeSpan.FromSeconds(options.Value.DispatchIntervalSeconds), stoppingToken);
        }
    }

    public async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IWebhookOutboxRepository>();
        var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();

        var pending = await outboxRepo.GetPendingAsync(50, ct);
        if (pending.Count == 0) return;

        foreach (var entry in pending)
        {
            var webhook = await webhookRepo.GetByIdAsync(entry.WebhookId, ct);
            if (webhook is null)
            {
                logger.LogWarning("Webhook {WebhookId} not found for outbox entry {EntryId}", entry.WebhookId, entry.Id);
                continue;
            }

            await DispatchAsync(entry, webhook.Url, webhook.Secret, outboxRepo, ct);
        }
    }

    private async Task DispatchAsync(
        PendingOutboxEntry entry,
        string url,
        string secret,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var httpClient = httpClientFactory.CreateClient("WebhookDispatcher");

        try
        {
            var signature = ComputeHmacSignature(entry.Payload, secret);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(entry.Payload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("X-Chronith-Event", entry.EventType);
            request.Headers.TryAddWithoutValidation("X-Chronith-Delivery", entry.Id.ToString());
            request.Headers.TryAddWithoutValidation("X-Chronith-Signature", $"sha256={signature}");

            var response = await httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                await outboxRepo.MarkDeliveredAsync(entry.Id, now, ct);
                logger.LogInformation("Delivered outbox entry {EntryId} to {Url}", entry.Id, url);
            }
            else
            {
                logger.LogWarning("Non-success {Status} from {Url} for entry {EntryId}",
                    (int)response.StatusCode, url, entry.Id);
                await MarkFailureAsync(entry, now, outboxRepo, ct);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "HTTP error delivering outbox entry {EntryId} to {Url}", entry.Id, url);
            await MarkFailureAsync(entry, now, outboxRepo, ct);
        }
    }

    private static async Task MarkFailureAsync(
        PendingOutboxEntry entry,
        DateTimeOffset now,
        IWebhookOutboxRepository outboxRepo,
        CancellationToken ct)
    {
        var newAttemptCount = entry.AttemptCount + 1;
        var isFinal = newAttemptCount >= MaxAttempts;
        DateTimeOffset? nextRetryAt = isFinal
            ? null
            : now.Add(BackOffSchedule[newAttemptCount - 1]);

        await outboxRepo.MarkFailedAttemptAsync(entry.Id, newAttemptCount, nextRetryAt, isFinal, ct);
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var message = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, message);
        return Convert.ToHexStringLower(hash);
    }
}
