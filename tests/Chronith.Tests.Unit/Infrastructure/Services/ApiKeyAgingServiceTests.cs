using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class ApiKeyAgingServiceTests
{
    private static (
        ApiKeyAgingService Service,
        ITenantRepository TenantRepo,
        IApiKeyRepository ApiKeyRepo,
        ILogger<ApiKeyAgingService> Logger)
        BuildSut(
            IReadOnlyList<Tenant>? tenants = null,
            IReadOnlyList<TenantApiKey>? keys = null,
            int thresholdDays = 90,
            int checkIntervalHours = 24)
    {
        var tenantRepo = Substitute.For<ITenantRepository>();
        var apiKeyRepo = Substitute.For<IApiKeyRepository>();

        tenantRepo
            .ListAllAsync(Arg.Any<CancellationToken>())
            .Returns((tenants ?? []).ToList().AsReadOnly() as IReadOnlyList<Tenant>
                     ?? new List<Tenant>().AsReadOnly());

        if (tenants is { Count: > 0 })
        {
            foreach (var tenant in tenants)
            {
                apiKeyRepo
                    .ListByTenantAsync(tenant.Id, Arg.Any<CancellationToken>())
                    .Returns((keys ?? []).ToList().AsReadOnly() as IReadOnlyList<TenantApiKey>
                             ?? new List<TenantApiKey>().AsReadOnly());
            }
        }

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITenantRepository)).Returns(tenantRepo);
        serviceProvider.GetService(typeof(IApiKeyRepository)).Returns(apiKeyRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var options = Options.Create(new ApiKeyAgingOptions
        {
            ThresholdDays = thresholdDays,
            CheckIntervalHours = checkIntervalHours
        });

        var logger = Substitute.For<ILogger<ApiKeyAgingService>>();
        var healthTracker = Substitute.For<IBackgroundServiceHealthTracker>();

        var sut = new ApiKeyAgingService(scopeFactory, options, healthTracker, logger);

        return (sut, tenantRepo, apiKeyRepo, logger);
    }

    [Fact]
    public async Task CheckAgingAsync_WithNoTenants_DoesNotQueryApiKeys()
    {
        var (sut, _, apiKeyRepo, _) = BuildSut(tenants: []);

        await sut.CheckAgingAsync(CancellationToken.None);

        await apiKeyRepo.DidNotReceive().ListByTenantAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAgingAsync_KeyOlderThanThreshold_LogsWarning()
    {
        const int thresholdDays = 90;
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");
        var keyId = Guid.NewGuid();
        var oldKey = new TenantApiKey
        {
            Id = keyId,
            TenantId = tenant.Id,
            KeyHash = "abc123",
            Description = "Old key",
            Scopes = [ApiKeyScope.TenantWrite],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-(thresholdDays + 1))
        };

        var (sut, _, _, logger) = BuildSut(
            tenants: [tenant],
            keys: [oldKey],
            thresholdDays: thresholdDays);

        await sut.CheckAgingAsync(CancellationToken.None);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(keyId.ToString())),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CheckAgingAsync_KeyYoungerThanThreshold_DoesNotLogWarning()
    {
        const int thresholdDays = 90;
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");
        var recentKey = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            KeyHash = "abc123",
            Description = "Recent key",
            Scopes = [ApiKeyScope.TenantWrite],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };

        var (sut, _, _, logger) = BuildSut(
            tenants: [tenant],
            keys: [recentKey],
            thresholdDays: thresholdDays);

        await sut.CheckAgingAsync(CancellationToken.None);

        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CheckAgingAsync_RevokedKey_DoesNotLogWarning()
    {
        const int thresholdDays = 90;
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");
        var revokedKey = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            KeyHash = "abc123",
            Description = "Revoked key",
            Scopes = [ApiKeyScope.TenantWrite],
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-200)
        };
        revokedKey.Revoke();

        var (sut, _, _, logger) = BuildSut(
            tenants: [tenant],
            keys: [revokedKey],
            thresholdDays: thresholdDays);

        await sut.CheckAgingAsync(CancellationToken.None);

        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
