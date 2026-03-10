using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IAuditEntryRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct);
    Task<AuditEntry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> QueryAsync(
        Guid tenantId,
        string? entityType,
        Guid? entityId,
        string? userId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct);
    Task<int> DeleteExpiredAsync(Guid tenantId, DateTimeOffset before, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetDistinctTenantIdsAsync(CancellationToken ct);
}
