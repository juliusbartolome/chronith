using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantSubscriptionRepository : ITenantSubscriptionRepository
{
    private readonly ChronithDbContext _db;

    public TenantSubscriptionRepository(ChronithDbContext db) => _db = db;

    public async Task<TenantSubscription?> GetActiveByTenantIdAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var activeStatuses = new[]
        {
            SubscriptionStatus.Active.ToString(),
            SubscriptionStatus.Trialing.ToString(),
            SubscriptionStatus.PastDue.ToString(),
        };

        var entity = await _db.TenantSubscriptions
            .TagWith("GetActiveByTenantIdAsync — TenantSubscriptionRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && activeStatuses.Contains(s.Status))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : TenantSubscriptionEntityMapper.ToDomain(entity);
    }

    public async Task AddAsync(TenantSubscription subscription, CancellationToken ct = default)
    {
        var entity = TenantSubscriptionEntityMapper.ToEntity(subscription);
        await _db.TenantSubscriptions.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(TenantSubscription subscription, CancellationToken ct = default)
    {
        var entity = await _db.TenantSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id, ct);

        if (entity is null) return;

        entity.Status = subscription.Status.ToString();
        entity.TrialEndsAt = subscription.TrialEndsAt;
        entity.CurrentPeriodStart = subscription.CurrentPeriodStart;
        entity.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
        entity.PaymentProviderSubscriptionId = subscription.PaymentProviderSubscriptionId;
        entity.CancelledAt = subscription.CancelledAt;
        entity.CancelReason = subscription.CancelReason;
        entity.PlanId = subscription.PlanId;
    }
}
