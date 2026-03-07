using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IWaitlistRepository
{
    Task<WaitlistEntry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistEntry>> ListBySlotAsync(Guid tenantId, Guid bookingTypeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task<WaitlistEntry?> GetNextWaitingAsync(Guid tenantId, Guid bookingTypeId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    Task AddAsync(WaitlistEntry entry, CancellationToken ct = default);
    Task UpdateAsync(WaitlistEntry entry, CancellationToken ct = default);
}
