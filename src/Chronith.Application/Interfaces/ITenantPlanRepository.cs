using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantPlanRepository
{
    Task<IReadOnlyList<TenantPlan>> GetActivePlansAsync(CancellationToken ct = default);
    Task<TenantPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
