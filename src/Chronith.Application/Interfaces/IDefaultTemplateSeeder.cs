namespace Chronith.Application.Interfaces;

public interface IDefaultTemplateSeeder
{
    Task SeedForEventTypeAsync(Guid tenantId, string eventType, CancellationToken cancellationToken = default);
    Task SeedAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
