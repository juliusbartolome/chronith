using Chronith.Application.Options;
using Chronith.Infrastructure.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure;

public class InMemoryRateLimitStoreTests
{
    [Fact]
    public void GetPermitLimit_WithNoOverride_ReturnsAuthenticatedDefault()
    {
        var options = new RateLimitingOptions
        {
            Authenticated = new PolicyConfig { PermitLimit = 300, WindowSeconds = 60 }
        };
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
            Authenticated = new PolicyConfig { PermitLimit = 300, WindowSeconds = 60 },
            TenantOverrides = new Dictionary<string, TenantOverride>
            {
                [tenantId] = new TenantOverride { PermitLimit = 1000 }
            }
        };
        var store = new InMemoryRateLimitStore(Options.Create(options));

        var limit = store.GetPermitLimit(tenantId);

        limit.Should().Be(1000);
    }

    [Fact]
    public void GetPermitLimit_WithUnknownTenant_ReturnsAuthenticatedDefault()
    {
        var options = new RateLimitingOptions
        {
            Authenticated = new PolicyConfig { PermitLimit = 300, WindowSeconds = 60 },
            TenantOverrides = new Dictionary<string, TenantOverride>
            {
                ["other-tenant"] = new TenantOverride { PermitLimit = 1000 }
            }
        };
        var store = new InMemoryRateLimitStore(Options.Create(options));

        var limit = store.GetPermitLimit("unknown-tenant");

        limit.Should().Be(300);
    }
}
