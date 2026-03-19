using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class StaffMemberRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    ILogger<StaffMemberRepository> logger)
    : IStaffMemberRepository
{
    private string DecryptEmail(string? value)
    {
        if (value is null) return string.Empty;
        try { return encryptionService.Decrypt(value) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("StaffMember.Email could not be decrypted — " +
                "treating as legacy plaintext row.");
            return value;
        }
    }

    public async Task<StaffMember?> GetByIdAsync(
        Guid tenantId, Guid staffId, CancellationToken ct = default)
    {
        var entity = await db.StaffMembers
            .TagWith("GetByIdAsync — StaffMemberRepository")
            .AsNoTracking()
            .Include(s => s.AvailabilityWindows)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == staffId, ct);

        if (entity is null) return null;
        entity.Email = DecryptEmail(entity.Email);
        return StaffMemberEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<StaffMember>> ListAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.StaffMembers
            .TagWith("ListAsync — StaffMemberRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(s => s.AvailabilityWindows)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            e.Email = DecryptEmail(e.Email);
            return StaffMemberEntityMapper.ToDomain(e);
        }).ToList();
    }

    public async Task<IReadOnlyList<StaffMember>> ListByBookingTypeAsync(
        Guid tenantId, Guid bookingTypeId, CancellationToken ct = default)
    {
        var entities = await db.BookingTypeStaffAssignments
            .TagWith("ListByBookingTypeAsync — StaffMemberRepository")
            .AsNoTracking()
            .Where(a => a.BookingTypeId == bookingTypeId)
            .Join(
                db.StaffMembers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(s => s.AvailabilityWindows)
                    .Where(s => s.TenantId == tenantId && !s.IsDeleted),
                a => a.StaffMemberId,
                s => s.Id,
                (_, s) => s)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return entities.Select(e =>
        {
            e.Email = DecryptEmail(e.Email);
            return StaffMemberEntityMapper.ToDomain(e);
        }).ToList();
    }

    public async Task AddAsync(StaffMember staff, CancellationToken ct = default)
    {
        var entity = StaffMemberEntityMapper.ToEntity(staff);
        entity.Email = encryptionService.Encrypt(entity.Email) ?? string.Empty;
        await db.StaffMembers.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(StaffMember staff, CancellationToken ct = default)
    {
        var entity = await db.StaffMembers
            .FirstOrDefaultAsync(s => s.Id == staff.Id, ct);

        if (entity is null) return;

        entity.Name = staff.Name;
        entity.Email = encryptionService.Encrypt(staff.Email) ?? string.Empty;
        entity.IsActive = staff.IsActive;
        entity.IsDeleted = staff.IsDeleted;

        // Replace availability windows: delete existing, add new
        await db.StaffAvailabilityWindows
            .Where(w => w.StaffMemberId == staff.Id)
            .ExecuteDeleteAsync(ct);

        var newWindows = staff.AvailabilityWindows
            .Select(w => new StaffAvailabilityWindowEntity
            {
                Id = Guid.NewGuid(),
                StaffMemberId = staff.Id,
                DayOfWeek = (int)w.DayOfWeek,
                StartTime = w.StartTime,
                EndTime = w.EndTime
            }).ToList();

        if (newWindows.Count > 0)
        {
            await db.StaffAvailabilityWindows.AddRangeAsync(newWindows, ct);
        }
    }

    public async Task AssignToBookingTypeAsync(
        Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default)
    {
        var exists = await db.BookingTypeStaffAssignments
            .AnyAsync(a => a.BookingTypeId == bookingTypeId && a.StaffMemberId == staffMemberId, ct);

        if (!exists)
        {
            await db.BookingTypeStaffAssignments.AddAsync(new BookingTypeStaffAssignmentEntity
            {
                BookingTypeId = bookingTypeId,
                StaffMemberId = staffMemberId
            }, ct);
        }
    }

    public async Task RemoveFromBookingTypeAsync(
        Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default)
    {
        await db.BookingTypeStaffAssignments
            .Where(a => a.BookingTypeId == bookingTypeId && a.StaffMemberId == staffMemberId)
            .ExecuteDeleteAsync(ct);
    }

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        db.StaffMembers
            .TagWith("CountByTenantAsync — StaffMemberRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive, ct);
}
