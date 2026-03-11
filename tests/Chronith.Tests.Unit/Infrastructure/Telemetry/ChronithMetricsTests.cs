using System.Diagnostics.Metrics;
using Chronith.Infrastructure.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Unit.Infrastructure.Telemetry;

public sealed class ChronithMetricsTests
{
    private static (ChronithMetrics metrics, ServiceProvider provider) CreateMetrics()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        return (new ChronithMetrics(meterFactory), provider);
    }

    [Fact]
    public void MeterName_IsChronithApi()
    {
        ChronithMetrics.MeterName.Should().Be("Chronith.API");
    }

    [Fact]
    public void RecordBookingCreated_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;
        string? kindTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.bookings.created")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                    if (tag.Key == "booking.kind") kindTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordBookingCreated("tenant-1", "TimeSlot");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-1");
        kindTag.Should().Be("TimeSlot");
    }

    [Fact]
    public void RecordBookingConfirmed_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.bookings.confirmed")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordBookingConfirmed("tenant-2");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-2");
    }

    [Fact]
    public void RecordBookingCancelled_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.bookings.cancelled")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordBookingCancelled("tenant-3");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-3");
    }

    [Fact]
    public void RecordPaymentProcessed_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;
        string? providerTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.payments.processed")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                    if (tag.Key == "payment.provider") providerTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordPaymentProcessed("tenant-1", "PayMongo");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-1");
        providerTag.Should().Be("PayMongo");
    }

    [Fact]
    public void RecordWebhookDispatched_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.webhooks.dispatched")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordWebhookDispatched("tenant-1");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-1");
    }

    [Fact]
    public void RecordNotificationSent_IncrementsCounter_WithCorrectTags()
    {
        long recorded = 0;
        string? tenantTag = null;
        string? channelTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.notifications.sent")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                    if (tag.Key == "notification.channel") channelTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordNotificationSent("tenant-1", "Email");
        }

        recorded.Should().Be(1);
        tenantTag.Should().Be("tenant-1");
        channelTag.Should().Be("Email");
    }

    [Fact]
    public void RecordAvailabilityDuration_RecordsHistogram_WithCorrectValueAndTags()
    {
        double recorded = 0;
        string? tenantTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.availability.duration_ms")
            {
                recorded = value;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenantTag = tag.Value?.ToString();
                }
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordAvailabilityDuration("tenant-1", 123.45);
        }

        recorded.Should().Be(123.45);
        tenantTag.Should().Be("tenant-1");
    }

    [Fact]
    public void RecordBookingCreated_MultipleCalls_EachEmitsCorrectValue()
    {
        var recordings = new List<(long value, string? tenant, string? kind)>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ChronithMetrics.MeterName)
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            if (inst.Name == "chronith.bookings.created")
            {
                string? tenant = null, kind = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "tenant.id") tenant = tag.Value?.ToString();
                    if (tag.Key == "booking.kind") kind = tag.Value?.ToString();
                }
                recordings.Add((value, tenant, kind));
            }
        });
        listener.Start();

        var (metrics, provider) = CreateMetrics();
        using (provider)
        {
            metrics.RecordBookingCreated("tenant-1", "TimeSlot");
            metrics.RecordBookingCreated("tenant-2", "Calendar");
        }

        recordings.Should().HaveCount(2);
        recordings[0].Should().Be((1L, "tenant-1", "TimeSlot"));
        recordings[1].Should().Be((1L, "tenant-2", "Calendar"));
    }
}
