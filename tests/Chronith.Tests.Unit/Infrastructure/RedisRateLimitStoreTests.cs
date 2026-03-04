using Chronith.Application.Options;
using Chronith.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure;

public class RedisRateLimitStoreTests
{
    [Fact]
    public void GetPermitLimit_WithNoOverride_ReturnsDefault()
    {
        var opts = new RateLimitingOptions { DefaultPermitLimit = 300 };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit("any-tenant").Should().Be(300);
    }

    [Fact]
    public void GetPermitLimit_WithTenantOverride_ReturnsOverride()
    {
        const string tenantId = "tenant-abc";
        var opts = new RateLimitingOptions
        {
            DefaultPermitLimit = 300,
            TenantOverrides = new Dictionary<string, TenantRateLimitOverride>
            {
                [tenantId] = new TenantRateLimitOverride { PermitLimit = 5000 }
            }
        };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit(tenantId).Should().Be(5000);
    }

    [Fact]
    public void GetPermitLimit_WithUnknownTenant_ReturnsDefault()
    {
        var opts = new RateLimitingOptions
        {
            DefaultPermitLimit = 100,
            TenantOverrides = new Dictionary<string, TenantRateLimitOverride>
            {
                ["other"] = new TenantRateLimitOverride { PermitLimit = 999 }
            }
        };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit("unknown").Should().Be(100);
    }
}
