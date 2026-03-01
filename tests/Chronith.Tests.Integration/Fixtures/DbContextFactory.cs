using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Integration.Fixtures;

public static class DbContextFactory
{
    public static async Task<ChronithDbContext> CreateAsync(
        string connectionString,
        Guid tenantId,
        bool applyMigrations = false)
    {
        var options = new DbContextOptionsBuilder<ChronithDbContext>()
            .UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", "chronith"))
            .Options;

        var tenantContext = new StubTenantContext(tenantId);
        var context = new ChronithDbContext(options, tenantContext);

        if (applyMigrations)
            await context.Database.MigrateAsync();

        return context;
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid TenantId { get; }
        public string UserId => "test-user";
        public string Role => "TenantAdmin";

        public StubTenantContext(Guid tenantId) => TenantId = tenantId;
    }
}
