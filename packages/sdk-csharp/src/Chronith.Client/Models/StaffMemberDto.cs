namespace Chronith.Client.Models;

public sealed record StaffMemberDto(
    Guid Id,
    string Name,
    string Email,
    Guid? TenantUserId,
    bool IsActive,
    IReadOnlyList<StaffAvailabilityWindowDto> AvailabilityWindows
);

public sealed record StaffAvailabilityWindowDto(
    string DayOfWeek,
    string StartTime,
    string EndTime
);
