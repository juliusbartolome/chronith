namespace Chronith.Application.DTOs;

// ── Booking Analytics ────────────────────────────────────────────────────────

public sealed record BookingAnalyticsDto(
    string Period,
    int Total,
    Dictionary<string, int> ByStatus,
    IReadOnlyList<BookingTypeCountDto> ByBookingType,
    IReadOnlyList<StaffCountDto> ByStaff,
    IReadOnlyList<TimeSeriesPointDto> TimeSeries);

public sealed record BookingTypeCountDto(
    string Slug,
    string Name,
    int Count);

public sealed record StaffCountDto(
    Guid StaffId,
    string Name,
    int Count);

public sealed record TimeSeriesPointDto(
    string Date,
    int Count);
