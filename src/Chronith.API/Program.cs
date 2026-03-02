using Chronith.API.HealthChecks;
using Chronith.API.Middleware;
using Chronith.Application;
using Chronith.Infrastructure;
using Chronith.Infrastructure.Persistence;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddAuthenticationJwtBearer(s => { s.SigningKey = builder.Configuration["Jwt:SigningKey"]!; })
    .AddAuthorization()
    .AddFastEndpoints()
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

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
   .UseAuthorization()
   .UseFastEndpoints(c => { c.Errors.UseProblemDetails(); });

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Name == "database" });

app.Run();

public partial class Program { }
