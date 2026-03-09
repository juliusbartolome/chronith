namespace Chronith.API.Middleware;

public sealed class VersionRedirectMiddleware(RequestDelegate next)
{
    private static readonly string[] PassThroughPrefixes = ["/v1/", "/v1", "/health", "/openapi.json", "/swagger"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        foreach (var prefix in PassThroughPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
        }

        var queryString = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value
            : string.Empty;

        context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
        context.Response.Headers.Location = $"/v1{path}{queryString}";
    }
}
