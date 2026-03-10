using Chronith.Application.Constants;
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

public sealed class UserIdEnricherTests
{
    private static LogEvent CreateLogEvent() =>
        new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            MessageTemplate.Empty,
            []);

    [Fact]
    public void Enrich_WhenHttpContextAvailable_AddsUserIdAndUserRoleProperties()
    {
        // Arrange
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.UserId.Returns("user-abc");
        tenantContext.Role.Returns("TenantAdmin");

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITenantContext)).Returns(tenantContext);

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var enricher = new UserIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();
        propertyFactory.CreateProperty("UserId", "user-abc", false)
            .Returns(new LogEventProperty("UserId", new ScalarValue("user-abc")));
        propertyFactory.CreateProperty("UserRole", "TenantAdmin", false)
            .Returns(new LogEventProperty("UserRole", new ScalarValue("TenantAdmin")));

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties["UserId"].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("user-abc");
        logEvent.Properties.Should().ContainKey("UserRole");
        logEvent.Properties["UserRole"].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("TenantAdmin");
    }

    [Fact]
    public void Enrich_WhenHttpContextIsNull_DoesNotAddUserIdOrUserRoleProperties()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var enricher = new UserIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
        logEvent.Properties.Should().NotContainKey("UserRole");
    }
}

public sealed class CorrelationIdEnricherTests
{
    private static LogEvent CreateLogEvent() =>
        new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            MessageTemplate.Empty,
            []);

    [Fact]
    public void Enrich_WhenCorrelationIdInItems_AddsCorrelationIdProperty()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextConstants.CorrelationIdKey] = correlationId;

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        var enricher = new CorrelationIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();
        propertyFactory.CreateProperty(HttpContextConstants.CorrelationIdKey, correlationId, false)
            .Returns(new LogEventProperty(HttpContextConstants.CorrelationIdKey, new ScalarValue(correlationId)));

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().ContainKey(HttpContextConstants.CorrelationIdKey);
        logEvent.Properties[HttpContextConstants.CorrelationIdKey].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be(correlationId);
    }

    [Fact]
    public void Enrich_WhenHttpContextIsNull_DoesNotAddCorrelationIdProperty()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        var enricher = new CorrelationIdEnricher(httpContextAccessor);
        var logEvent = CreateLogEvent();
        var propertyFactory = Substitute.For<ILogEventPropertyFactory>();

        // Act
        enricher.Enrich(logEvent, propertyFactory);

        // Assert
        logEvent.Properties.Should().NotContainKey(HttpContextConstants.CorrelationIdKey);
    }
}
