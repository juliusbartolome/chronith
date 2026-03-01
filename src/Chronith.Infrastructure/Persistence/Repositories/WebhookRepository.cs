using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class WebhookRepository : IWebhookRepository
{
    private readonly ChronithDbContext _db;

    public WebhookRepository(ChronithDbContext db) => _db = db;

    public async Task<IReadOnlyList<Webhook>> ListAsync(
        Guid tenantId, Guid bookingTypeId, CancellationToken ct = default)
    {
        var entities = await _db.Webhooks
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.BookingTypeId == bookingTypeId)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Webhook?> GetByIdAsync(
        Guid tenantId, Guid webhookId, CancellationToken ct = default)
    {
        var entity = await _db.Webhooks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == webhookId, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(Webhook webhook, CancellationToken ct = default)
    {
        var entity = new WebhookEntity
        {
            Id = webhook.Id,
            TenantId = webhook.TenantId,
            BookingTypeId = webhook.BookingTypeId,
            Url = webhook.Url,
            Secret = webhook.Secret,
            IsDeleted = webhook.IsDeleted
        };
        await _db.Webhooks.AddAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid webhookId, CancellationToken ct = default)
    {
        var entity = await _db.Webhooks
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == webhookId, ct);

        if (entity is not null)
        {
            entity.IsDeleted = true;
        }
    }

    private static Webhook MapToDomain(WebhookEntity e)
    {
        var domain = Webhook.Create(e.TenantId, e.BookingTypeId, e.Url, e.Secret);
        // Overwrite Id with stored value via reflection
        var idProp = typeof(Webhook).GetProperty(nameof(Webhook.Id),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        idProp?.SetValue(domain, e.Id);
        return domain;
    }
}
