using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Providers;

public interface IDbProviderConfigurator
{
    void Configure(DbContextOptionsBuilder options, string connectionString);
}
