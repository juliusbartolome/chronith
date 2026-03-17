using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IRecurrenceRuleRepository
{
    Task<RecurrenceRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecurrenceRule>> GetByBookingTypeIdAsync(Guid bookingTypeId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurrenceRule>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(RecurrenceRule rule, CancellationToken ct = default);
    void Update(RecurrenceRule rule);

    /// <summary>
    /// Cross-tenant query for background services. Ignores query filters
    /// and explicitly filters out soft-deleted records.
    /// </summary>
    Task<IReadOnlyList<RecurrenceRule>> GetAllActiveAcrossTenantsAsync(CancellationToken ct = default);
}
