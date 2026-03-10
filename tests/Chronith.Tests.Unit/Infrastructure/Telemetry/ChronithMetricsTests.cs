using System.Diagnostics.Metrics;
using Chronith.Infrastructure.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Unit.Infrastructure.Telemetry;

public sealed class ChronithMetricsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;

    public ChronithMetricsTests()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        _serviceProvider = services.BuildServiceProvider();
        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void RecordBookingCreated_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordBookingCreated("tenant-1", "TimeSlot");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordBookingConfirmed_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordBookingConfirmed("tenant-1");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordBookingCancelled_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordBookingCancelled("tenant-1");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordPaymentProcessed_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordPaymentProcessed("tenant-1", "PayMongo");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordWebhookDispatched_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordWebhookDispatched("tenant-1");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordNotificationSent_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordNotificationSent("tenant-1", "Email");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordAvailabilityDuration_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () => metrics.RecordAvailabilityDuration("tenant-1", 42.5);

        act.Should().NotThrow();
    }

    [Fact]
    public void MeterName_IsChronithApi()
    {
        ChronithMetrics.MeterName.Should().Be("Chronith.API");
    }

    [Fact]
    public void RecordBookingCreated_WithMultipleTenants_DoesNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () =>
        {
            metrics.RecordBookingCreated("tenant-1", "TimeSlot");
            metrics.RecordBookingCreated("tenant-2", "Calendar");
            metrics.RecordBookingCreated("tenant-1", "Calendar");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void AllRecordMethods_WithMultipleCalls_DoNotThrow()
    {
        var metrics = new ChronithMetrics(_meterFactory);

        var act = () =>
        {
            for (var i = 0; i < 10; i++)
            {
                metrics.RecordBookingCreated("tenant-1", "TimeSlot");
                metrics.RecordBookingConfirmed("tenant-1");
                metrics.RecordBookingCancelled("tenant-1");
                metrics.RecordPaymentProcessed("tenant-1", "Stub");
                metrics.RecordWebhookDispatched("tenant-1");
                metrics.RecordNotificationSent("tenant-1", "SMS");
                metrics.RecordAvailabilityDuration("tenant-1", i * 1.5);
            }
        };

        act.Should().NotThrow();
    }
}
