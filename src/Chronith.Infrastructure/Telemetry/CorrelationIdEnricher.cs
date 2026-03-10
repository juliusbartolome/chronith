using Chronith.Application.Constants;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Chronith.Infrastructure.Telemetry;

public sealed class CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        if (httpContext.Items[HttpContextConstants.CorrelationIdKey] is not string correlationId) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(HttpContextConstants.CorrelationIdKey, correlationId));
    }
}
