using Chronith.API.HealthChecks;
using Chronith.API.Middleware;
using Chronith.Application;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure;
using Chronith.Infrastructure.Auth;
using Chronith.Infrastructure.Persistence;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAuthenticationJwtBearer(s => { s.SigningKey = builder.Configuration["Jwt:SigningKey"]!; })
    .AddAuthorization()
    .AddFastEndpoints()
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.SchemeLabel,
        _ => { });

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var tenantId = ctx.User?.FindFirst("tenant_id")?.Value ?? "anonymous";
        var store = ctx.RequestServices.GetRequiredService<IRateLimitStore>();
        var rateLimitOpts = ctx.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var permitLimit = store.GetPermitLimit(tenantId);

        return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromSeconds(rateLimitOpts.DefaultWindowSeconds),
            PermitLimit = permitLimit,
            QueueLimit = rateLimitOpts.QueueLimit,
            SegmentsPerWindow = 6,
        });
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 429,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. See the Retry-After header for when you can retry.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
        }, ct);
    };
});

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

var app = builder.Build();

// Run EF Core migrations on startup so the schema is always up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(ExceptionHandlingMiddleware.Configure);
app.UseAuthentication()
   .UseAuthorization();

app.UseRateLimiter();

// Append X-RateLimit-* informational headers on successful responses
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if (ctx.Response.StatusCode != StatusCodes.Status429TooManyRequests)
        {
            var store = ctx.RequestServices.GetRequiredService<IRateLimitStore>();
            var opts = ctx.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
            var tenantId = ctx.User?.FindFirst("tenant_id")?.Value ?? "anonymous";
            var limit = store.GetPermitLimit(tenantId);
            var windowEnd = DateTimeOffset.UtcNow.AddSeconds(opts.DefaultWindowSeconds);

            ctx.Response.Headers["X-RateLimit-Limit"] = limit.ToString(CultureInfo.InvariantCulture);
            ctx.Response.Headers["X-RateLimit-Reset"] =
                windowEnd.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseFastEndpoints(c => { c.Errors.UseProblemDetails(); });

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Name == "database" });

app.Run();

public partial class Program { }
