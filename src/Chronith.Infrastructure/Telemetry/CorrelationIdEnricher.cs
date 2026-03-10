using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Chronith.Infrastructure.Telemetry;

public sealed class CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    // Mirrors CorrelationIdMiddleware.ItemKey — kept in sync manually
    private const string ItemKey = "CorrelationId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        if (httpContext.Items[ItemKey] is not string correlationId) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
}
