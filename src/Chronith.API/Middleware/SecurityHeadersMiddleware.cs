using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.API.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IOptions<CspOptions> cspOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var csp = cspOptions.Value;
        var policy = $"default-src {csp.DefaultSrc}; script-src {csp.ScriptSrc}; style-src {csp.StyleSrc}; img-src {csp.ImgSrc}; connect-src {csp.ConnectSrc}; frame-ancestors {csp.FrameAncestors}";

        context.Response.Headers["Content-Security-Policy"] = policy;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await next(context);
    }
}
