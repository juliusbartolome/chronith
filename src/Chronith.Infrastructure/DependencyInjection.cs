using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Infrastructure.Providers;
using Chronith.Infrastructure.Services;
using Chronith.Infrastructure.TenantContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chronith.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]!;

        var configurator = provider switch
        {
            "PostgreSQL" => (IDbProviderConfigurator)new PostgreSqlConfigurator(),
            _ => throw new NotSupportedException($"Database provider '{provider}' is not supported.")
        };

        services.AddDbContext<ChronithDbContext>((sp, options) =>
        {
            configurator.Configure(options, connectionString);
        });

        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ITenantContext, JwtTenantContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBookingTypeRepository, BookingTypeRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IWebhookOutboxRepository, WebhookOutboxRepository>();
        services.AddHostedService<WebhookDispatcherService>();
        services.AddHttpClient("WebhookDispatcher");
        services.Configure<WebhookDispatcherOptions>(configuration.GetSection("Webhooks"));

        return services;
    }
}
