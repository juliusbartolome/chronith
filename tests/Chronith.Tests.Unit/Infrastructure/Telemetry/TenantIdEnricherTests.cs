using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Telemetry;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Serilog.Core;
using Serilog.Events;

namespace Chronith.Tests.Unit.Infrastructure.Telemetry;

public sealed class TenantIdEnricherTests
{
    private static LogEvent CreateLogEvent() =>
        new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            MessageTemplate.Empty,
            []);

    [Fact]
    public void Enrich_WhenHttpContextAvailable_AddsTenantIdProperty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenantId);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITenantContext)).Returns(tenantContext);

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var enricher = new TenantIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();
        propertyFactory.CreateProperty("TenantId", tenantId.ToString(), false)
            .Returns(new LogEventProperty("TenantId", new ScalarValue(tenantId.ToString())));

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("TenantId");
        logEvent.Properties["TenantId"].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be(tenantId.ToString());
    }

    [Fact]
    public void Enrich_WhenHttpContextIsNull_DoesNotAddTenantIdProperty()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var enricher = new TenantIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey("TenantId");
    }
}
