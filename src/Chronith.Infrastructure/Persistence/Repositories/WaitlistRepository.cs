using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class WaitlistRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<WaitlistRepository> logger)
    : IWaitlistRepository
{
    private string DecryptCustomerEmail(string? value)
    {
        if (value is null) return string.Empty;
        try { return encryptionService.Decrypt(value) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("WaitlistEntry.CustomerEmail could not be decrypted — " +
                "treating as legacy plaintext row.");
            return value;
        }
    }

    public async Task<WaitlistEntry?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.WaitlistEntries
            .TagWith("GetByIdAsync — WaitlistRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

        if (entity is null) return null;
        entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
        return WaitlistEntryEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> ListBySlotAsync(
        Guid tenantId, Guid bookingTypeId,
        DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var entities = await db.WaitlistEntries
            .TagWith("ListBySlotAsync — WaitlistRepository")
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                        && w.BookingTypeId == bookingTypeId
                        && w.DesiredStart < end
                        && w.DesiredEnd > start)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            e.CustomerEmail = DecryptCustomerEmail(e.CustomerEmail);
            return WaitlistEntryEntityMapper.ToDomain(e);
        }).ToList();
    }

    public async Task<WaitlistEntry?> GetNextWaitingAsync(
        Guid tenantId, Guid bookingTypeId,
        DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        var entity = await db.WaitlistEntries
            .TagWith("GetNextWaitingAsync — WaitlistRepository")
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                        && w.BookingTypeId == bookingTypeId
                        && w.Status == WaitlistStatus.Waiting
                        && w.DesiredStart < end
                        && w.DesiredEnd > start)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (entity is null) return null;
        entity.CustomerEmail = DecryptCustomerEmail(entity.CustomerEmail);
        return WaitlistEntryEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<WaitlistEntry>> GetExpiredOffersAsync(
        DateTimeOffset now, CancellationToken ct = default)
    {
        var entities = await db.WaitlistEntries
            .TagWith("GetExpiredOffersAsync — WaitlistRepository")
            .AsNoTracking()
            .Where(w => w.Status == WaitlistStatus.Offered && w.ExpiresAt <= now)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            e.CustomerEmail = DecryptCustomerEmail(e.CustomerEmail);
            return WaitlistEntryEntityMapper.ToDomain(e);
        }).ToList();
    }

    public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        var entity = WaitlistEntryEntityMapper.ToEntity(entry);
        entity.CustomerEmail = encryptionService.Encrypt(entity.CustomerEmail) ?? string.Empty;
        await db.WaitlistEntries.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        await db.WaitlistEntries
            .Where(w => w.Id == entry.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Status, entry.Status)
                .SetProperty(w => w.OfferedAt, entry.OfferedAt)
                .SetProperty(w => w.ExpiresAt, entry.ExpiresAt)
                .SetProperty(w => w.IsDeleted, entry.IsDeleted),
                ct);
    }
}
