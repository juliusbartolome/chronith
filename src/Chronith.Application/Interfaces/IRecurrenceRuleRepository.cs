using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IRecurrenceRuleRepository
{
    Task<RecurrenceRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecurrenceRule>> GetByBookingTypeIdAsync(Guid bookingTypeId, CancellationToken ct = default);
    Task AddAsync(RecurrenceRule rule, CancellationToken ct = default);
    void Update(RecurrenceRule rule);
}
