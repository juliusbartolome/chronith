using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class WebhookRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<WebhookRepository> logger)
    : IWebhookRepository
{
    public async Task<IReadOnlyList<Webhook>> ListAsync(
        Guid tenantId, Guid bookingTypeId, CancellationToken ct = default)
    {
        var entities = await db.Webhooks
            .TagWith("ListAsync — WebhookRepository")
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.BookingTypeId == bookingTypeId)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Webhook?> GetByIdAsync(
        Guid tenantId, Guid webhookId, CancellationToken ct = default)
    {
        var entity = await db.Webhooks
            .TagWith("GetByIdAsync — WebhookRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == webhookId, ct);

        return entity is null ? null : MapToDomain(entity);
    }

    /// <summary>
    /// Retrieves a webhook by ID only, ignoring tenant filter.
    /// Used by the cross-tenant WebhookDispatcherService background worker.
    /// </summary>
    public async Task<Webhook?> GetByIdCrossTenantAsync(Guid webhookId, CancellationToken ct = default)
    {
        var entity = await db.Webhooks
            .TagWith("GetByIdCrossTenantAsync — WebhookRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == webhookId && !w.IsDeleted, ct);

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
            Secret = encryptionService.Encrypt(webhook.Secret) ?? string.Empty,
            IsDeleted = webhook.IsDeleted
        };
        await db.Webhooks.AddAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid webhookId, CancellationToken ct = default)
    {
        var entity = await db.Webhooks
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == webhookId, ct);

        if (entity is not null)
        {
            entity.IsDeleted = true;
        }
    }

    private Webhook MapToDomain(WebhookEntity e)
    {
        var decryptedSecret = DecryptSecret(e.Secret);
        var domain = Webhook.Create(e.TenantId, e.BookingTypeId, e.Url, decryptedSecret);
        // Overwrite Id with stored value via reflection
        var idProp = typeof(Webhook).GetProperty(nameof(Webhook.Id),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        idProp?.SetValue(domain, e.Id);
        return domain;
    }

    private string DecryptSecret(string? secret)
    {
        if (secret is null) return string.Empty;
        try { return encryptionService.Decrypt(secret) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            // Legacy row: secret column contains plaintext (pre-encryption migration)
            // or was seeded without the version prefix.
            // Return as-is; next write will encrypt it.
            logger.LogWarning(
                "Webhook secret could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return secret;
        }
    }
}
