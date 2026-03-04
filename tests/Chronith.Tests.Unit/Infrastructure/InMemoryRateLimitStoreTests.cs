using Chronith.Application.Options;
using Chronith.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure;

public class InMemoryRateLimitStoreTests
{
    [Fact]
    public void GetPermitLimit_WithNoOverride_ReturnsDefault()
    {
        var options = new RateLimitingOptions { DefaultPermitLimit = 300 };
        var store = new InMemoryRateLimitStore(Options.Create(options));

        var limit = store.GetPermitLimit("any-tenant-id");

        limit.Should().Be(300);
    }

    [Fact]
    public void GetPermitLimit_WithTenantOverride_ReturnsOverride()
    {
        const string tenantId = "tenant-abc";
        var options = new RateLimitingOptions
        {
            DefaultPermitLimit = 300,
            TenantOverrides = new Dictionary<string, TenantRateLimitOverride>
            {
                [tenantId] = new TenantRateLimitOverride { PermitLimit = 1000 }
            }
        };
        var store = new InMemoryRateLimitStore(Options.Create(options));

        var limit = store.GetPermitLimit(tenantId);

        limit.Should().Be(1000);
    }

    [Fact]
    public void GetPermitLimit_WithUnknownTenant_ReturnsDefault()
    {
        var options = new RateLimitingOptions
        {
            DefaultPermitLimit = 300,
            TenantOverrides = new Dictionary<string, TenantRateLimitOverride>
            {
                ["other-tenant"] = new TenantRateLimitOverride { PermitLimit = 1000 }
            }
        };
        var store = new InMemoryRateLimitStore(Options.Create(options));

        var limit = store.GetPermitLimit("unknown-tenant");

        limit.Should().Be(300);
    }
}
