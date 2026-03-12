using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantSettingsRepository(ChronithDbContext db) : ITenantSettingsRepository
{
    public async Task<TenantSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entity = await db.TenantSettings
            .TagWith("GetByTenantIdAsync — TenantSettingsRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);

        return entity is null ? null : TenantSettingsEntityMapper.ToDomain(entity);
    }

    public async Task<TenantSettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default)
    {
        var existing = await GetByTenantIdAsync(tenantId, ct);
        if (existing is not null) return existing;

        var newSettings = TenantSettings.Create(tenantId);
        await AddAsync(newSettings, ct);
        await db.SaveChangesAsync(ct);
        return newSettings;
    }

    public async Task AddAsync(TenantSettings settings, CancellationToken ct = default)
    {
        var entity = TenantSettingsEntityMapper.ToEntity(settings);
        await db.TenantSettings.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(TenantSettings settings, CancellationToken ct = default)
    {
        var entity = await db.TenantSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == settings.TenantId && !s.IsDeleted, ct);

        if (entity is null) return;

        entity.LogoUrl = settings.LogoUrl;
        entity.PrimaryColor = settings.PrimaryColor;
        entity.AccentColor = settings.AccentColor;
        entity.CustomDomain = settings.CustomDomain;
        entity.BookingPageEnabled = settings.BookingPageEnabled;
        entity.WelcomeMessage = settings.WelcomeMessage;
        entity.TermsUrl = settings.TermsUrl;
        entity.PrivacyUrl = settings.PrivacyUrl;
        entity.UpdatedAt = settings.UpdatedAt;
    }
}
