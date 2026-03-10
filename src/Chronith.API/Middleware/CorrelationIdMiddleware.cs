using Chronith.Application.Constants;

namespace Chronith.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = HttpContextConstants.CorrelationIdKey;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await next(context);
    }
}
