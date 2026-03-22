using System.Text;
using Chronith.Application.Interfaces;
using Chronith.API.Authorization;
using Chronith.API.HealthChecks;
using Chronith.API.Middleware;
using Chronith.API.Processors;
using Chronith.Application;
using Chronith.Application.Options;
using Chronith.Infrastructure;
using Chronith.Infrastructure.Auth;
using Chronith.Infrastructure.Persistence;
using Chronith.Application.Telemetry;
using Chronith.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
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

// QuestPDF community license (free for open-source / internal use)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
    .AddAuthenticationJwtBearer(s =>
    {
        // Primary signing key for token generation and fallback validation
        var primaryKey = builder.Configuration["Jwt:SigningKey"]!;

        // If Jwt:SigningKeys is configured, use the first entry as primary
        var configuredKeys = builder.Configuration.GetSection("Jwt:SigningKeys").Get<string[]>();
        s.SigningKey = configuredKeys is { Length: > 0 } ? configuredKeys[0] : primaryKey;
    })
    .AddFastEndpoints()
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<BackgroundServiceHealthCheck>("background-services");

// Register JsonStringEnumConverter via ASP.NET Core's minimal-API JSON options so
// FastEndpoints picks it up through its IOptions<JsonOptions> copy on startup.
// This avoids mutating FastEndpoints' process-global static JsonSerializerOptions
// (Config.SerOpts) inside UseFastEndpoints, which is not safe when multiple
// WebApplicationFactory instances start concurrently in tests.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthorization(options =>
{
    // Register one named policy per scope for use with Policies("scope:xxx") on endpoints
    foreach (var scope in Chronith.Application.Models.ApiKeyScope.All)
        options.AddPolicy($"scope:{scope}",
            p => p.AddRequirements(new ApiKeyScopeRequirement(scope)));
});

builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyScopeHandler>();

// Wire multi-key JWT validation: all keys in Jwt:SigningKeys are accepted for signature verification
// This supports zero-downtime key rotation — tokens signed with old keys remain valid.
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
    Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
    options =>
    {
        var primaryKey = builder.Configuration["Jwt:SigningKey"]!;
        var configuredKeys = builder.Configuration.GetSection("Jwt:SigningKeys").Get<string[]>();
        var allKeys = configuredKeys is { Length: > 0 } ? configuredKeys : [primaryKey];

        options.TokenValidationParameters.IssuerSigningKeys = allKeys
            .Select(k => (SecurityKey)new SymmetricSecurityKey(Encoding.UTF8.GetBytes(k)))
            .ToList();
    });

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
        s.Version = "v0.8";
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
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value ?? "anonymous";
        var limit = rl.TenantOverrides.TryGetValue(tenantId, out var o) && o.PermitLimit.HasValue
            ? o.PermitLimit.Value
            : rl.Authenticated.PermitLimit;
        var window = rl.TenantOverrides.TryGetValue(tenantId, out var ow) && ow.WindowSeconds.HasValue
            ? ow.WindowSeconds.Value
            : rl.Authenticated.WindowSeconds;
        return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(window),
            QueueLimit = rl.QueueLimit,
            SegmentsPerWindow = 6,
        });
    });

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
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value ?? "anonymous";
        var limit = rl.TenantOverrides.TryGetValue(tenantId, out var o) && o.PermitLimit.HasValue
            ? o.PermitLimit.Value
            : rl.Export.PermitLimit;
        var window = rl.TenantOverrides.TryGetValue(tenantId, out var ow) && ow.WindowSeconds.HasValue
            ? ow.WindowSeconds.Value
            : rl.Export.WindowSeconds;
        return RateLimitPartition.GetSlidingWindowLimiter(tenantId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromSeconds(window),
            QueueLimit = rl.QueueLimit,
            SegmentsPerWindow = 6,
        });
    });

    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        var resp = context.HttpContext.Response;
        resp.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            resp.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        else
            resp.Headers.RetryAfter = "60";
        // Use WriteAsync so we control Content-Type exactly (WriteAsJsonAsync sets application/json).
        resp.ContentType = "application/problem+json";
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = 429,
            title = "Too Many Requests",
            detail = "Rate limit exceeded. See the Retry-After header for when you can retry.",
            type = "https://tools.ietf.org/html/rfc6585#section-4",
        });
        await resp.WriteAsync(body, ct);
    };
});

builder.Host.UseSerilog((ctx, sp, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.With(sp.GetRequiredService<TenantIdEnricher>())
    .Enrich.With(sp.GetRequiredService<UserIdEnricher>())
    .Enrich.With(sp.GetRequiredService<CorrelationIdEnricher>()));

var app = builder.Build();

// Guard: reject placeholder secrets in any environment except Development.
// This catches misconfigured deployments before they can start serving traffic.
if (!app.Environment.IsDevelopment())
{
    const string jwtPlaceholder = "REPLACE_WITH_SECRET__run_openssl_rand_-hex_32";

    var jwtKey = app.Configuration["Jwt:SigningKey"];

    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == jwtPlaceholder)
        throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Generate a key with: openssl rand -hex 32");

    var encryptionVersion = app.Configuration["Security:EncryptionKeyVersion"];
    var encryptionKey = app.Configuration[$"Security:KeyVersions:{encryptionVersion}"];
    if (string.IsNullOrEmpty(encryptionVersion) ||
        encryptionVersion.StartsWith("SET_VIA_") ||
        string.IsNullOrEmpty(encryptionKey) ||
        encryptionKey.StartsWith("SET_VIA_") ||
        encryptionKey == "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")
        throw new InvalidOperationException(
            $"Security:KeyVersions is not configured. Set Security:EncryptionKeyVersion and " +
            $"Security:KeyVersions:{encryptionVersion ?? "<version>"} via Azure App Service settings or Key Vault references.");
}

// Run EF Core migrations on startup so the schema is always up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChronithDbContext>();
    await db.Database.MigrateAsync();

    // Seed default subscription plans (idempotent)
    var planSeeder = scope.ServiceProvider.GetRequiredService<IPlanSeeder>();
    await planSeeder.SeedAsync();
}

app.UseExceptionHandler(ExceptionHandlingMiddleware.Configure);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseAuthentication()
   .UseAuthorization();

app.UseRateLimiter();

app.UseSwaggerGen();

app.UseMiddleware<VersionRedirectMiddleware>();

app.UseMiddleware<RequestBodyHashMiddleware>();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "v1";
    c.Errors.UseProblemDetails();
    // JsonStringEnumConverter is registered via ConfigureHttpJsonOptions above;
    // FastEndpoints copies it from IOptions<JsonOptions> on startup.
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
