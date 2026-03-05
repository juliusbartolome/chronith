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
            .AsNoTracking()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.TenantId == tenantId && bt.Slug == slug, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    public async Task<BookingType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .AsNoTracking()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.TenantId == tenantId && bt.Id == id, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    /// <inheritdoc cref="IBookingTypeRepository.GetByIdAsync(Guid, CancellationToken)"/>
    public async Task<BookingType?> GetByIdAsync(Guid bookingTypeId, CancellationToken ct = default)
    {
        var entity = await _db.BookingTypes
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.Id == bookingTypeId, ct);

        return entity is null ? null : BookingTypeEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<BookingType>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var entities = await _db.BookingTypes
            .AsNoTracking()
            .Include(bt => bt.AvailabilityWindows)
            .Where(bt => bt.TenantId == tenantId)
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
            .AsNoTracking()
            .AnyAsync(bt => bt.TenantId == tenantId && bt.Slug == slug, ct);

    public async Task<BookingTypeMetrics> GetTypeMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var counts = await _db.BookingTypes
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
        var entity = await _db.BookingTypes
            .Include(bt => bt.AvailabilityWindows)
            .FirstOrDefaultAsync(bt => bt.Id == bookingType.Id, ct);

        if (entity is null) return;

        // Remove old windows
        _db.AvailabilityWindows.RemoveRange(entity.AvailabilityWindows);

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

        foreach (var w in updated.AvailabilityWindows)
        {
            entity.AvailabilityWindows.Add(w);
        }
    }
}
