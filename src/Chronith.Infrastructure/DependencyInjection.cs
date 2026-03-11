using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Application.Services;
using Chronith.Infrastructure.Auth;
using Chronith.Infrastructure.Caching;
using Chronith.Infrastructure.Notifications;
using Chronith.Infrastructure.Payments;
using Chronith.Infrastructure.Payments.PayMongo;
using Chronith.Infrastructure.Persistence;
using Chronith.Infrastructure.Persistence.Repositories;
using Chronith.Infrastructure.Providers;
using Chronith.Infrastructure.RateLimiting;
using Chronith.Infrastructure.Security;
using Chronith.Infrastructure.Services;
using Chronith.Infrastructure.Services.Audit;
using Chronith.Infrastructure.Services.Notifications;
using Chronith.Infrastructure.Telemetry;
using Chronith.Infrastructure.TenantContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

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
        services.AddScoped<IStaffMemberRepository, StaffMemberRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<ITimeBlockRepository, TimeBlockRepository>();
        services.AddScoped<IWebhookOutboxRepository, WebhookOutboxRepository>();
        services.AddScoped<INotificationConfigRepository, NotificationConfigRepository>();
        services.AddScoped<IBookingReminderRepository, BookingReminderRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<ITenantUserRepository, TenantUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerRefreshTokenRepository, CustomerRefreshTokenRepository>();
        services.AddScoped<ITenantAuthConfigRepository, TenantAuthConfigRepository>();
        services.AddScoped<IRecurrenceRuleRepository, RecurrenceRuleRepository>();
        services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<IDefaultTemplateSeeder, DefaultTemplateSeeder>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddScoped<IAuditSnapshotResolver, BookingSnapshotResolver>();
        services.AddScoped<IAuditSnapshotResolver, BookingTypeSnapshotResolver>();
        services.AddScoped<IAuditSnapshotResolver, StaffMemberSnapshotResolver>();
        services.AddScoped<IAuditSnapshotResolver, TenantSnapshotResolver>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IOidcTokenValidator, OidcTokenValidator>();
        services.AddHostedService<WebhookDispatcherService>();
        services.AddHostedService<WaitlistPromotionService>();
        services.AddHostedService<NotificationDispatcherService>();
        services.AddHostedService<ReminderSchedulerService>();
        services.AddHostedService<RecurringBookingGeneratorService>();
        services.AddHostedService<IdempotencyCleanupService>();
        services.AddHostedService<AuditRetentionService>();
        var httpTimeoutSeconds = configuration.GetValue("Webhooks:HttpTimeoutSeconds", 10);
        services.AddHttpClient("WebhookDispatcher", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(httpTimeoutSeconds);
        });
        services.Configure<WebhookDispatcherOptions>(configuration.GetSection("Webhooks"));
        services.Configure<WaitlistPromotionOptions>(configuration.GetSection("WaitlistPromotion"));
        services.Configure<NotificationDispatcherOptions>(configuration.GetSection("NotificationDispatcher"));
        services.Configure<ReminderSchedulerOptions>(configuration.GetSection("ReminderScheduler"));
        services.Configure<RecurringBookingGeneratorOptions>(configuration.GetSection("RecurringBookings"));
        services.Configure<IdempotencyOptions>(configuration.GetSection("Idempotency"));
        services.Configure<AuditRetentionOptions>(configuration.GetSection("AuditRetention"));

        // Notification channels
        services.Configure<SmtpOptions>(configuration.GetSection("Notifications:Smtp"));
        services.Configure<TwilioOptions>(configuration.GetSection("Notifications:Twilio"));
        services.Configure<FirebasePushOptions>(configuration.GetSection("Notifications:Firebase"));
        services.AddSingleton<INotificationChannel, SmtpEmailChannel>();
        services.AddSingleton<INotificationChannel, TwilioSmsChannel>();
        services.AddSingleton<INotificationChannel, FirebasePushChannel>();
        services.AddSingleton<NotificationChannelFactory>();
        services.AddHttpClient("Twilio");

        // Payment providers
        services.Configure<PaymentsOptions>(configuration.GetSection("Payments"));
        services.AddSingleton<IPaymentProvider, StubPaymentProvider>();
        services.AddHttpClient("PayMongo", client =>
        {
            client.BaseAddress = new Uri("https://api.paymongo.com");
        });
        services.Configure<PayMongoOptions>(configuration.GetSection("PaymentProviders:PayMongo"));
        services.AddSingleton<IPaymentProvider, PayMongoProvider>();
        services.AddHttpClient("Maya");
        services.Configure<MayaOptions>(configuration.GetSection("PaymentProviders:Maya"));
        services.AddSingleton<IPaymentProvider, MayaProvider>();
        services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();

        // Encryption
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.AddSingleton<IEncryptionService, EncryptionService>();

        // Rate limiting
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        // Telemetry
        services.AddMetrics();
        services.AddSingleton<ChronithMetrics>();
        services.AddSingleton<IBackgroundServiceHealthTracker, BackgroundServiceHealthTracker>();
        services.AddSingleton<TenantIdEnricher>();
        services.AddSingleton<UserIdEnricher>();
        services.AddSingleton<CorrelationIdEnricher>();

        // Redis (optional)
        var redisEnabled = configuration.GetValue<bool>("Redis:Enabled");
        if (redisEnabled)
        {
            var redisConnectionString = configuration["Redis:ConnectionString"]!;
            services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
            services.AddSingleton<IRedisCacheService, RedisCacheService>();
            services.AddSingleton<IRateLimitStore, RedisRateLimitStore>();
        }
        else
        {
            services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
        }

        return services;
    }
}
