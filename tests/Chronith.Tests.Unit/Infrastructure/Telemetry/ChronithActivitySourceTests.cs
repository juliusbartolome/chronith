using System.Diagnostics;
using Chronith.Application.Telemetry;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Telemetry;

public sealed class ChronithActivitySourceTests
{
    private static ActivityListener CreateListener(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ChronithActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public void StartBookingStateTransition_CreatesActivityWithCorrectName()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartBookingStateTransition("Create", tenantId, bookingId);

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.booking.state_transition");
        activity.Tags.Should().Contain(t => t.Key == "booking.operation" && t.Value == "Create");
        activity.Tags.Should().Contain(t => t.Key == "tenant.id" && t.Value == tenantId.ToString());
        activity.Tags.Should().Contain(t => t.Key == "booking.id" && t.Value == bookingId.ToString());
    }

    [Fact]
    public void StartPaymentProcess_CreatesActivityWithCorrectName()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartPaymentProcess(tenantId, "PayMongo");

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.payment.process");
        activity.Tags.Should().Contain(t => t.Key == "payment.provider" && t.Value == "PayMongo");
    }

    [Fact]
    public void StartWebhookDispatch_CreatesActivityWithCorrectTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartWebhookDispatch(tenantId, webhookId);

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.webhook.dispatch");
        activity.Tags.Should().Contain(t => t.Key == "webhook.id" && t.Value == webhookId.ToString());
    }

    [Fact]
    public void StartNotificationDispatch_CreatesActivityWithCorrectTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartNotificationDispatch(tenantId, "email");

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.notification.dispatch");
        activity.Tags.Should().Contain(t => t.Key == "notification.channel" && t.Value == "email");
    }

    [Fact]
    public void StartAvailabilityCompute_CreatesActivityWithCorrectTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartAvailabilityCompute(tenantId, "haircut-30min");

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.availability.compute");
        activity.Tags.Should().Contain(t => t.Key == "booking_type.slug" && t.Value == "haircut-30min");
    }

    [Fact]
    public void StartRecurringGenerate_CreatesActivityWithCorrectTags()
    {
        var captured = new List<Activity>();
        using var listener = CreateListener(captured);

        var tenantId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        using var activity = ChronithActivitySource.StartRecurringGenerate(tenantId, ruleId);

        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("chronith.recurring.generate");
        activity.Tags.Should().Contain(t => t.Key == "recurrence_rule.id" && t.Value == ruleId.ToString());
    }
}
