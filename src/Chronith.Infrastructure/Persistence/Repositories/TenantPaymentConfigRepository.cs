using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantPaymentConfigRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<TenantPaymentConfigRepository> logger)
    : ITenantPaymentConfigRepository
{
    public async Task<TenantPaymentConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.TenantPaymentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (entity is null) return null;
        entity.Settings = DecryptSettings(entity.Settings);
        return TenantPaymentConfigEntityMapper.ToDomain(entity);
    }

    public async Task<TenantPaymentConfig?> GetActiveByProviderNameAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        var entity = await db.TenantPaymentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.ProviderName == providerName &&
                c.IsActive &&
                !c.IsDeleted, ct);
        if (entity is null) return null;
        entity.Settings = DecryptSettings(entity.Settings);
        return TenantPaymentConfigEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<TenantPaymentConfig>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantPaymentConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderBy(c => c.ProviderName).ThenBy(c => c.Label)
            .ToListAsync(ct);
        foreach (var e in entities) e.Settings = DecryptSettings(e.Settings);
        return entities.Select(TenantPaymentConfigEntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<TenantPaymentConfig>> ListActiveByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantPaymentConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.ProviderName).ThenBy(c => c.Label)
            .ToListAsync(ct);
        foreach (var e in entities) e.Settings = DecryptSettings(e.Settings);
        return entities.Select(TenantPaymentConfigEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(TenantPaymentConfig config, CancellationToken ct = default)
    {
        var entity = TenantPaymentConfigEntityMapper.ToEntity(config);
        entity.Settings = encryptionService.Encrypt(config.Settings) ?? "{}";
        await db.TenantPaymentConfigs.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(TenantPaymentConfig config, CancellationToken ct = default)
    {
        var encryptedSettings = encryptionService.Encrypt(config.Settings) ?? "{}";
        await db.TenantPaymentConfigs
            .Where(c => c.Id == config.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Label, config.Label)
                .SetProperty(c => c.IsActive, config.IsActive)
                .SetProperty(c => c.IsDeleted, config.IsDeleted)
                .SetProperty(c => c.Settings, encryptedSettings)
                .SetProperty(c => c.PublicNote, config.PublicNote)
                .SetProperty(c => c.QrCodeUrl, config.QrCodeUrl)
                .SetProperty(c => c.UpdatedAt, config.UpdatedAt),
                ct);
    }

    public async Task DeactivateAllByProviderNameAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        await db.TenantPaymentConfigs
            .Where(c => c.TenantId == tenantId && c.ProviderName == providerName && !c.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.TenantPaymentConfigs
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsDeleted, true)
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }

    private string DecryptSettings(string? settings)
    {
        if (settings is null) return "{}";
        try { return encryptionService.Decrypt(settings) ?? "{}"; }
        catch (FormatException)
        {
            // Legacy row: settings column contains plaintext JSON (pre-migration).
            // Return as-is; next write will encrypt it.
            logger.LogWarning(
                "Payment config settings could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return settings;
        }
    }
}
