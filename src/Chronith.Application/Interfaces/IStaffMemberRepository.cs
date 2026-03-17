using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IStaffMemberRepository
{
    Task<StaffMember?> GetByIdAsync(Guid tenantId, Guid staffId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffMember>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<StaffMember>> ListByBookingTypeAsync(Guid tenantId, Guid bookingTypeId, CancellationToken ct = default);
    Task AddAsync(StaffMember staff, CancellationToken ct = default);
    Task UpdateAsync(StaffMember staff, CancellationToken ct = default);
    Task AssignToBookingTypeAsync(Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default);
    Task RemoveFromBookingTypeAsync(Guid bookingTypeId, Guid staffMemberId, CancellationToken ct = default);

    /// <summary>COUNT of active staff members for a tenant. Used by plan enforcement.</summary>
    Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
