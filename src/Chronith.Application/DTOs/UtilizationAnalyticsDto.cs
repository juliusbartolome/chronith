namespace Chronith.Application.DTOs;

// ── Utilization Analytics ────────────────────────────────────────────────────

public sealed record UtilizationAnalyticsDto(
    string Period,
    IReadOnlyList<BookingTypeUtilizationDto> ByBookingType,
    IReadOnlyList<StaffUtilizationDto> ByStaff);

public sealed record BookingTypeUtilizationDto(
    string Slug,
    int TotalSlots,
    int BookedSlots,
    decimal UtilizationRate);

public sealed record StaffUtilizationDto(
    Guid StaffId,
    string Name,
    int TotalSlots,
    int BookedSlots,
    decimal UtilizationRate);
