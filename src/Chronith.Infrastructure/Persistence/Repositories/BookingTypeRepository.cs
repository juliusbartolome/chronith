using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class BookingTypeRepository : IBookingTypeRepository
{
    private readonly ChronithDbContext _db;

    public BookingTypeRepository(ChronithDbContext db) => _db = db;

    public async Task<BookingType?> GetBySlugAsync(Guid tenantId, string slug, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .TagWith("GetBySlugAsync(tenantId, slug) — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.TenantId == tenantId && !bt.IsDeleted && bt.Slug == slug, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    public async Task<BookingType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .TagWith("GetByIdAsync(tenantId, id) — BookingTypeRepository")
            .AsNoTracking()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.TenantId == tenantId && bt.Id == id, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    /// <inheritdoc cref="IBookingTypeRepository.GetByIdAsync(Guid, CancellationToken)"/>
    public async Task<BookingType?> GetByIdAsync(Guid bookingTypeId, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .TagWith("GetByIdAsync(bookingTypeId) — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.Id == bookingTypeId, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    /// <inheritdoc cref="IBookingTypeRepository.GetByIdAcrossTenantsAsync"/>
    public async Task<BookingType?> GetByIdAcrossTenantsAsync(Guid bookingTypeId, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .TagWith("GetByIdAcrossTenantsAsync — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.Id == bookingTypeId && !bt.IsDeleted, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    /// <inheritdoc cref="IBookingTypeRepository.GetBySlugAsync(string, CancellationToken)"/>
    public async Task<BookingType?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .TagWith("GetBySlugAsync(slug) — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.Slug == slug && !bt.IsDeleted, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<BookingType>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entities = await _db.BookingTypes
            .TagWith("ListAsync — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .Where(bt => bt.TenantId == tenantId && !bt.IsDeleted)
            .OrderBy(bt => bt.Name)
            .ToListAsync(ct);

        return entities.Select(BookingTypeEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(BookingType bookingType, CancellationToken ct = default)
    {
        var entity = BookingTypeEntityMapper.ToEntity(bookingType);
        await _db.BookingTypes.AddAsync(entity, ct);
    }

    public async Task<bool> SlugExistsAsync(Guid tenantId, string slug, CancellationToken ct = default)
        => await _db.BookingTypes
            .TagWith("SlugExistsAsync — BookingTypeRepository")
            .AsNoTracking()
            .AnyAsync(bt => bt.TenantId == tenantId && bt.Slug == slug, ct);

    public async Task<BookingTypeMetrics> GetTypeMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var counts = await _db.BookingTypes
            .TagWith("GetTypeMetricsAsync — BookingTypeRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(bt => bt.TenantId == tenantId)
            .GroupBy(bt => bt.IsDeleted)
            .Select(g => new { IsDeleted = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var active = counts.FirstOrDefault(c => !c.IsDeleted)?.Count ?? 0;
        var archived = counts.FirstOrDefault(c => c.IsDeleted)?.Count ?? 0;

        return new BookingTypeMetrics(active, archived);
    }

    public async Task UpdateAsync(BookingType bookingType, CancellationToken ct = default)
    {
        // Load without Include so the change tracker only tracks the parent row,
        // avoiding xmin concurrency conflicts caused by navigation collection mutations.
        var entity = await _db.BookingTypes
            .FirstOrDefaultAsync(bt => bt.Id == bookingType.Id, ct);

        if (entity is null) return;

        var updated = BookingTypeEntityMapper.ToEntity(bookingType);
        entity.Name = updated.Name;
        entity.Capacity = updated.Capacity;
        entity.PaymentMode = updated.PaymentMode;
        entity.PaymentProvider = updated.PaymentProvider;
        entity.IsDeleted = updated.IsDeleted;
        entity.DurationMinutes = updated.DurationMinutes;
        entity.BufferBeforeMinutes = updated.BufferBeforeMinutes;
        entity.BufferAfterMinutes = updated.BufferAfterMinutes;
        entity.AvailableDays = updated.AvailableDays;
        entity.CustomerCallbackUrl = updated.CustomerCallbackUrl;
        entity.CustomerCallbackSecret = updated.CustomerCallbackSecret;

        // Replace windows: delete all existing, queue new ones for insert via SaveChanges.
        await _db.AvailabilityWindows
            .Where(w => w.BookingTypeId == bookingType.Id)
            .ExecuteDeleteAsync(ct);

        if (updated.AvailabilityWindows.Count > 0)
        {
            await _db.AvailabilityWindows.AddRangeAsync(updated.AvailabilityWindows, ct);
        }
    }
}
