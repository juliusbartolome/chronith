using Chronith.API.HealthChecks;
using Chronith.API.Middleware;
using Chronith.API.Processors;
using Chronith.Application;
using Chronith.Application.Options;
using Chronith.Infrastructure;
using Chronith.Infrastructure.Auth;
using Chronith.Infrastructure.Persistence;
using Chronith.Infrastructure.Telemetry;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NSwag;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var observabilityOptions = builder.Configuration
    .GetSection(ObservabilityOptions.SectionName)
    .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

builder.Services.Configure<CspOptions>(builder.Configuration.GetSection(CspOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var corsConfig = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
        if (corsConfig.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsConfig.AllowedOrigins);
            if (corsConfig.AllowCredentials)
                policy.AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin();
        }
        policy.WithHeaders(corsConfig.AllowedHeaders)
              .WithExposedHeaders(corsConfig.ExposedHeaders);
    });
});

var healthChecksBuilder = builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAuthenticationJwtBearer(s => { s.SigningKey = builder.Configuration["Jwt:SigningKey"]!; })
    .AddAuthorization()
    .AddFastEndpoints()
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<BackgroundServiceHealthCheck>("background-services");

if (builder.Configuration.GetValue<bool>("Redis:Enabled"))
{
    healthChecksBuilder.AddCheck<RedisHealthCheck>("redis");
}

var otelBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(observabilityOptions.ServiceName));

if (observabilityOptions.EnableTracing)
{
    otelBuilder.WithTracing(tracing =>
    {
        tracing
            .AddSource(ChronithActivitySource.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(observabilityOptions.OtlpEndpoint));
    });
}

if (observabilityOptions.EnableMetrics)
{
    otelBuilder.WithMetrics(metrics =>
    {
        metrics
            .AddMeter(ChronithMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(observabilityOptions.OtlpEndpoint))
            .AddPrometheusExporter();
    });
}

builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "Chronith API";
        s.Version = "v0.4";
        s.Description = "Multi-tenant booking engine REST API";
        s.PostProcess = doc =>
        {
            foreach (var tag in new[] { "Auth", "Bookings", "BookingTypes", "Availability", "Webhooks", "Tenant", "Payments" })
                doc.Tags.Add(new NSwag.OpenApiTag { Name = tag });
        };
        s.AddAuth("BearerAuth", new NSwag.OpenApiSecurityScheme
        {
            Type = NSwag.OpenApiSecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token.",
        });
        s.AddAuth("ApiKeyAuth", new NSwag.OpenApiSecurityScheme
        {
            Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
            Name = "X-Api-Key",
            In = NSwag.OpenApiSecurityApiKeyLocation.Header,
            Description = "Enter your API key.",
        });
    };
    o.EnableJWTBearerAuth = true;
});

builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.SchemeLabel,
        _ => { });

builder.Services.AddRateLimiter(options =>
{
    var rl = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();

    options.AddPolicy("Authenticated", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User.FindFirst("tenant_id")?.Value ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rl.Authenticated.PermitLimit,
                Window = TimeSpan.FromSeconds(rl.Authenticated.WindowSeconds),
                QueueLimit = rl.QueueLimit,
                SegmentsPerWindow = 6,
            }));

    options.AddPolicy("Public", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rl.Public.PermitLimit,
                Window = TimeSpan.FromSeconds(rl.Public.WindowSeconds),
                QueueLimit = rl.QueueLimit,
                SegmentsPerWindow = 6,
            }));

    options.AddPolicy("Auth", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rl.Auth.PermitLimit,
                Window = TimeSpan.FromSeconds(rl.Auth.WindowSeconds),
                QueueLimit = rl.QueueLimit,
                SegmentsPerWindow = 6,
            }));

    options.AddPolicy("Export", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.User.FindFirst("tenant_id")?.Value ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rl.Export.PermitLimit,
                Window = TimeSpan.FromSeconds(rl.Export.WindowSeconds),
                QueueLimit = rl.QueueLimit,
                SegmentsPerWindow = 6,
            }));

    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
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

builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.With(sp.GetRequiredService<TenantIdEnricher>())
    .Enrich.With(sp.GetRequiredService<UserIdEnricher>())
    .Enrich.With(sp.GetRequiredService<CorrelationIdEnricher>()));

var app = builder.Build();

// Run EF Core migrations on startup so the schema is always up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(ExceptionHandlingMiddleware.Configure);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthentication()
   .UseAuthorization();

app.UseRateLimiter();

app.UseSwaggerGen(c => c.Path = "/openapi.json");

app.UseMiddleware<VersionRedirectMiddleware>();

app.UseMiddleware<RequestBodyHashMiddleware>();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "v1";
    c.Errors.UseProblemDetails();
    c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
    c.Endpoints.Configurator = ep =>
    {
        ep.PreProcessor<IdempotencyPreProcessor>(Order.Before);
        ep.PostProcessor<IdempotencyPostProcessor>(Order.After);
    };
});

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Name is "database" or "background-services" or "redis",
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            }),
        });
        await ctx.Response.WriteAsync(result);
    },
});

if (observabilityOptions.EnableMetrics)
{
    app.MapPrometheusScrapingEndpoint("/metrics");
}

app.Run();

public partial class Program { }
