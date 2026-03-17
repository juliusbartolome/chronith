using Chronith.API.Middleware;
using Chronith.Application.Interfaces;
using FastEndpoints;

namespace Chronith.API.Processors;

public sealed class IdempotencyPreProcessor : IGlobalPreProcessor
{
    internal const string IdempotencyItemsKey = "__IdempotencyData";
    internal const string IdempotencyOriginalBodyKey = "__IdempotencyOriginalBody";

    public async Task PreProcessAsync(IPreProcessorContext ctx, CancellationToken ct)
    {
        var httpContext = ctx.HttpContext;

        // Only apply to POST requests
        if (!HttpMethods.IsPost(httpContext.Request.Method))
            return;

        // Read Idempotency-Key header — if absent, skip
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyHeader) ||
            string.IsNullOrWhiteSpace(idempotencyKeyHeader))
            return;

        var idempotencyKey = idempotencyKeyHeader.ToString();

        // Read tenant_id from JWT claims — if absent, skip
        var tenantIdClaim = httpContext.User?.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim) || !Guid.TryParse(tenantIdClaim, out var tenantId))
            return;

        // Read pre-computed body hash from RequestBodyHashMiddleware.
        // The middleware runs before FastEndpoints, so the body was read before model binding consumed it.
        var requestHash = httpContext.Items[RequestBodyHashMiddleware.RequestHashItemsKey] as string
            ?? string.Empty;

        var endpointRoute = httpContext.Request.Path.Value ?? string.Empty;

        // Look up existing key in repo
        var repo = httpContext.RequestServices.GetRequiredService<IIdempotencyKeyRepository>();
        var existing = await repo.GetByKeyAndRouteAsync(tenantId, idempotencyKey, endpointRoute, ct);

        if (existing is not null)
        {
            if (existing.MatchesRequest(requestHash))
            {
                // Replay cached response
                httpContext.Response.StatusCode = existing.ResponseStatusCode;
                httpContext.Response.ContentType = existing.ResponseContentType;
                await httpContext.Response.WriteAsync(existing.ResponseBody, ct);
                await httpContext.Response.CompleteAsync();
                return;
            }

            // Hash mismatch — same key, different body
            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 422,
                Title = "Idempotency key reuse with different request",
                Detail = "The Idempotency-Key has already been used with a different request body."
            }, ct);
            await httpContext.Response.CompleteAsync();
            return;
        }

        // Store data in HttpContext.Items for post-processor
        httpContext.Items[IdempotencyItemsKey] = new IdempotencyData(
            tenantId, idempotencyKey, endpointRoute, requestHash);

        // Wrap response body to capture it in post-processor
        var originalBodyStream = httpContext.Response.Body;
        var captureStream = new MemoryStream();
        httpContext.Response.Body = captureStream;
        httpContext.Items[IdempotencyOriginalBodyKey] = originalBodyStream;
    }
}

internal sealed record IdempotencyData(
    Guid TenantId,
    string Key,
    string EndpointRoute,
    string RequestHash);
