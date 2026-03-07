using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class StaffMemberMapper
{
    public static StaffMemberDto ToDto(this StaffMember staff) =>
        new(
            Id: staff.Id,
            Name: staff.Name,
            Email: staff.Email,
            TenantUserId: staff.TenantUserId,
            IsActive: staff.IsActive,
            AvailabilityWindows: staff.AvailabilityWindows
                .Select(w => new StaffAvailabilityWindowDto(
                    w.DayOfWeek, w.StartTime, w.EndTime))
                .ToList());
}
