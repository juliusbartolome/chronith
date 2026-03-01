using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by EF Core tools (migrations, scaffolding).
/// Not used at runtime.
/// </summary>
public sealed class ChronithDbContextFactory : IDesignTimeDbContextFactory<ChronithDbContext>
{
    public ChronithDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChronithDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=chronith;Username=chronith;Password=chronith",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "chronith"));

        // Provide a stub tenant context for design-time — not used during migrations
        var tenantContext = new DesignTimeTenantContext();

        return new ChronithDbContext(optionsBuilder.Options, tenantContext);
    }

    private sealed class DesignTimeTenantContext : Application.Interfaces.ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string UserId => "design-time";
        public string Role => "design-time";
    }
}
