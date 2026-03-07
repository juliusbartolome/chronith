using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITimeBlockRepository
{
    Task<IReadOnlyList<TimeBlock>> ListInRangeAsync(Guid tenantId, Guid? bookingTypeId, Guid? staffMemberId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task AddAsync(TimeBlock block, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
