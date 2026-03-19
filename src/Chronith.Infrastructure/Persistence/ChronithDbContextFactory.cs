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
        // Prefer the env var so CI and Docker environments can supply their own connection string.
        // Fall back to the local-dev default so `dotnet ef` works out-of-the-box without any setup.
        const string localDevDefault = "Host=localhost;Database=chronith;Username=chronith;Password=chronith";
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Chronith")
            ?? localDevDefault;

        var optionsBuilder = new DbContextOptionsBuilder<ChronithDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
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
