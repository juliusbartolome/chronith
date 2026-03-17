namespace Chronith.Application.DTOs;

public sealed record StaffMemberDto(
    Guid Id,
    string Name,
    string Email,
    Guid? TenantUserId,
    bool IsActive,
    IReadOnlyList<StaffAvailabilityWindowDto> AvailabilityWindows);

public sealed record StaffAvailabilityWindowDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);
