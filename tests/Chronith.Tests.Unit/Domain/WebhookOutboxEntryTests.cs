using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class WebhookOutboxEntryTests
{
    private static WebhookOutboxEntry CreateEntry() => new()
    {
        TenantId = Guid.NewGuid(),
        WebhookId = Guid.NewGuid(),
        BookingId = Guid.NewGuid(),
        EventType = "booking.confirmed",
        Payload = "{}",
    };

    [Fact]
    public void RecordSuccess_SetsDeliveredStatus()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;

        entry.RecordSuccess(now);

        entry.Status.Should().Be(OutboxStatus.Delivered);
        entry.DeliveredAt.Should().Be(now);
        entry.LastAttemptAt.Should().Be(now);
    }

    [Fact]
    public void RecordFailure_FirstAttempt_SetsPendingWithBackOff30s()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;

        entry.RecordFailure(now);

        entry.Status.Should().Be(OutboxStatus.Pending);
        entry.AttemptCount.Should().Be(1);
        entry.NextRetryAt.Should().BeCloseTo(now.AddSeconds(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordSuccess_OnFailedEntry_Throws()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < WebhookOutboxEntry.MaxAttempts; i++)
            entry.RecordFailure(now);

        var act = () => entry.RecordSuccess(now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(1, 30)]        // 30 seconds
    [InlineData(2, 120)]       // 2 minutes
    [InlineData(3, 600)]       // 10 minutes
    [InlineData(4, 3600)]      // 1 hour
    [InlineData(5, 14400)]     // 4 hours
    public void RecordFailure_BackOffSchedule_CorrectDelays(int attempt, int expectedSeconds)
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < attempt; i++)
            entry.RecordFailure(now);

        entry.NextRetryAt.Should().BeCloseTo(now.AddSeconds(expectedSeconds), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordFailure_MaxAttempts_SetsFailedStatus()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < WebhookOutboxEntry.MaxAttempts; i++)
            entry.RecordFailure(now);

        entry.Status.Should().Be(OutboxStatus.Failed);
        entry.AttemptCount.Should().Be(WebhookOutboxEntry.MaxAttempts);
        entry.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public void ResetForRetry_WhenStatusIsFailed_SetsStatusPendingAndClearsAttemptCount()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < WebhookOutboxEntry.MaxAttempts; i++)
            entry.RecordFailure(now);
        entry.Status.Should().Be(OutboxStatus.Failed);

        var before = DateTimeOffset.UtcNow;
        entry.ResetForRetry();

        entry.Status.Should().Be(OutboxStatus.Pending);
        entry.AttemptCount.Should().Be(0);
        entry.RetryRequestedAt.Should().NotBeNull();
        entry.RetryRequestedAt.Should().BeOnOrAfter(before);
        entry.NextRetryAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void ResetForRetry_WhenStatusIsNotFailed_ThrowsInvalidStateTransitionException()
    {
        var entry = CreateEntry();
        // entry starts as Pending

        var act = () => entry.ResetForRetry();

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
