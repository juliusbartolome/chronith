using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class WebhookOutboxCleanupServiceTests
{
    private static (WebhookOutboxCleanupService Service, IWebhookOutboxRepository Repo) BuildSut(
        int retentionDays = 30,
        int intervalHours = 6)
    {
        var repo = Substitute.For<IWebhookOutboxRepository>();
        repo.DeleteOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IWebhookOutboxRepository)).Returns(repo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var options = Options.Create(new WebhookOutboxCleanupOptions
        {
            RetentionDays = retentionDays,
            IntervalHours = intervalHours
        });

        var svc = new WebhookOutboxCleanupService(scopeFactory, options,
            NullLogger<WebhookOutboxCleanupService>.Instance);

        return (svc, repo);
    }

    [Fact]
    public void Options_DefaultRetentionDays_Is30()
    {
        var opts = new WebhookOutboxCleanupOptions();
        opts.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void Options_DefaultIntervalHours_Is6()
    {
        var opts = new WebhookOutboxCleanupOptions();
        opts.IntervalHours.Should().Be(6);
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_CallsDeleteOlderThan_WithCorrectCutoff()
    {
        var (svc, repo) = BuildSut(retentionDays: 30);

        await svc.PurgeExpiredEntriesAsync(CancellationToken.None);

        await repo.Received(1).DeleteOlderThanAsync(
            Arg.Is<DateTimeOffset>(d => d <= DateTimeOffset.UtcNow.AddDays(-29)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_WhenNothingDeleted_DoesNotLog()
    {
        var (svc, _) = BuildSut();

        // Should complete without throwing
        await svc.PurgeExpiredEntriesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_WhenRowsDeleted_CompletesSuccessfully()
    {
        var (svc, repo) = BuildSut(retentionDays: 7);
        repo.DeleteOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(42));

        await svc.PurgeExpiredEntriesAsync(CancellationToken.None);

        await repo.Received(1).DeleteOlderThanAsync(
            Arg.Is<DateTimeOffset>(d => d <= DateTimeOffset.UtcNow.AddDays(-6)),
            Arg.Any<CancellationToken>());
    }
}
