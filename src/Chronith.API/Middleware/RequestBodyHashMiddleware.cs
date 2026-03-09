using System.Security.Cryptography;
using System.Text;

namespace Chronith.API.Middleware;

/// <summary>
/// Enables request body buffering and computes a SHA-256 hash of the body
/// for POST requests that carry an Idempotency-Key header.
/// Must be registered BEFORE FastEndpoints so the body stream is still unconsumed.
/// </summary>
public sealed class RequestBodyHashMiddleware(RequestDelegate next)
{
    internal const string RequestHashItemsKey = "__IdempotencyRequestHash";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Headers.ContainsKey("Idempotency-Key"))
        {
            context.Request.EnableBuffering();

            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
            context.Items[RequestHashItemsKey] = hash;
        }

        await next(context);
    }
}
