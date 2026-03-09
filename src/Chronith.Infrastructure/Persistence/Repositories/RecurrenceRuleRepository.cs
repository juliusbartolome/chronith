using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class RecurrenceRuleRepository(ChronithDbContext db) : IRecurrenceRuleRepository
{
    public async Task<RecurrenceRule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.RecurrenceRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<RecurrenceRule>> GetByBookingTypeIdAsync(Guid bookingTypeId, CancellationToken ct = default)
    {
        var entities = await db.RecurrenceRules.AsNoTracking()
            .Where(r => r.BookingTypeId == bookingTypeId)
            .ToListAsync(ct);
        return entities.Select(e => e.ToDomain()).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<RecurrenceRule>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await db.RecurrenceRules.AsNoTracking()
            .ToListAsync(ct);
        return entities.Select(e => e.ToDomain()).ToList().AsReadOnly();
    }

    public async Task AddAsync(RecurrenceRule rule, CancellationToken ct = default) =>
        await db.RecurrenceRules.AddAsync(rule.ToEntity(), ct);

    public void Update(RecurrenceRule rule) =>
        db.RecurrenceRules.Update(rule.ToEntity());

    public async Task<IReadOnlyList<RecurrenceRule>> GetAllActiveAcrossTenantsAsync(CancellationToken ct = default)
    {
        var entities = await db.RecurrenceRules
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .ToListAsync(ct);
        return entities.Select(e => e.ToDomain()).ToList().AsReadOnly();
    }
}
