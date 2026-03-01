using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Providers;

public sealed class PostgreSqlConfigurator : IDbProviderConfigurator
{
    public void Configure(DbContextOptionsBuilder options, string connectionString)
        => options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "chronith"));
}
