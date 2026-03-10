using Chronith.Application.Options;
using Chronith.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure;

public class RedisRateLimitStoreTests
{
    [Fact]
    public void GetPermitLimit_WithNoOverride_ReturnsAuthenticatedDefault()
    {
        var opts = new RateLimitingOptions
        {
            Authenticated = new PolicyConfig { PermitLimit = 300, WindowSeconds = 60 }
        };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit("any-tenant").Should().Be(300);
    }

    [Fact]
    public void GetPermitLimit_WithTenantOverride_ReturnsOverride()
    {
        const string tenantId = "tenant-abc";
        var opts = new RateLimitingOptions
        {
            Authenticated = new PolicyConfig { PermitLimit = 300, WindowSeconds = 60 },
            TenantOverrides = new Dictionary<string, TenantOverride>
            {
                [tenantId] = new TenantOverride { PermitLimit = 5000 }
            }
        };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit(tenantId).Should().Be(5000);
    }

    [Fact]
    public void GetPermitLimit_WithUnknownTenant_ReturnsAuthenticatedDefault()
    {
        var opts = new RateLimitingOptions
        {
            Authenticated = new PolicyConfig { PermitLimit = 100, WindowSeconds = 60 },
            TenantOverrides = new Dictionary<string, TenantOverride>
            {
                ["other"] = new TenantOverride { PermitLimit = 999 }
            }
        };
        var store = new RedisRateLimitStore(Options.Create(opts));

        store.GetPermitLimit("unknown").Should().Be(100);
    }
}
