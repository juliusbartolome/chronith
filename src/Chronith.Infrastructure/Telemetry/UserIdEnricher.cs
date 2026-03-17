using Chronith.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Chronith.Infrastructure.Telemetry;

public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        var tenantContext = httpContext.RequestServices.GetService(typeof(ITenantContext)) as ITenantContext;
        if (tenantContext is null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", tenantContext.UserId));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserRole", tenantContext.Role));
    }
}
