namespace Chronith.Application.Interfaces;

public interface IPlanSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
