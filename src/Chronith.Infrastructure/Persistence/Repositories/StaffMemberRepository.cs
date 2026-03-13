using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class StaffMemberRepository : IStaffMemberRepository
{
    private readonly ChronithDbContext _db;

    public StaffMemberRepository(ChronithDbContext db) => _db = db;

    public async Task<StaffMember?> GetByIdAsync(
        Guid tenantId, Guid staffId, CancellationToken ct = default)
    {
        var entity = await _db.StaffMembers
            .TagWith("GetByIdAsync — StaffMemberRepository")
            .AsNoTracking()
            .Include(s => s.AvailabilityWindows)
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == staffId, ct);

        return entity is null ? null : StaffMemberEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<StaffMember>> ListAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await _db.StaffMembers
            .TagWith("ListAsync — StaffMemberRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(s => s.AvailabilityWindows)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return entities.Select(StaffMemberEntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<StaffMember>> ListByBookingTypeAsync(
        Guid tenantId, Guid bookingTypeId, CancellationToken ct = default)
    {
        var entities = await _db.BookingTypeStaffAssignments
            .TagWith("ListByBookingTypeAsync — StaffMemberRepository")
            .AsNoTracking()
            .Where(a => a.BookingTypeId == bookingTypeId)
            .Join(
                _db.StaffMembers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(s => s.AvailabilityWindows)
                    .Where(s => s.TenantId == tenantId && !s.IsDeleted),
                a => a.StaffMemberId,
                s => s.Id,
                (_, s) => s)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return entities.Select(StaffMemberEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(StaffMember staff, CancellationToken ct = default)
    {
        var entity = StaffMemberEntityMapper.ToEntity(staff);
        await _db.StaffMembers.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(StaffMember staff, CancellationToken ct = default)
    {
        var entity = await _db.StaffMembers
            .FirstOrDefaultAsync(s => s.Id == staff.Id, ct);

        if (entity is null) return;

        entity.Name = staff.Name;
        entity.Email = staff.Email;
        entity.IsActive = staff.IsActive;
        entity.IsDeleted = staff.IsDeleted;

        // Replace availability windows: delete existing, add new
        await _db.StaffAvailabilityWindows
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
            await _db.StaffAvailabilityWindows.AddRangeAsync(newWindows, ct);
        }
    }

    public async Task AssignToBookingTypeAsync(
        Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default)
    {
        var exists = await _db.BookingTypeStaffAssignments
            .AnyAsync(a => a.BookingTypeId == bookingTypeId && a.StaffMemberId == staffMemberId, ct);

        if (!exists)
        {
            await _db.BookingTypeStaffAssignments.AddAsync(new BookingTypeStaffAssignmentEntity
            {
                BookingTypeId = bookingTypeId,
                StaffMemberId = staffMemberId
            }, ct);
        }
    }

    public async Task RemoveFromBookingTypeAsync(
        Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default)
    {
        await _db.BookingTypeStaffAssignments
            .Where(a => a.BookingTypeId == bookingTypeId && a.StaffMemberId == staffMemberId)
            .ExecuteDeleteAsync(ct);
    }

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.StaffMembers
            .TagWith("CountByTenantAsync — StaffMemberRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
}
