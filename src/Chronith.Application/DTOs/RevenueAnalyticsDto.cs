namespace Chronith.Application.DTOs;

// ── Revenue Analytics ────────────────────────────────────────────────────────

public sealed record RevenueAnalyticsDto(
    string Period,
    long TotalCentavos,
    string Currency,
    IReadOnlyList<BookingTypeRevenueDto> ByBookingType,
    IReadOnlyList<RevenueTimeSeriesPointDto> TimeSeries);

public sealed record BookingTypeRevenueDto(
    string Slug,
    long TotalCentavos,
    int Count);

public sealed record RevenueTimeSeriesPointDto(
    string Date,
    long TotalCentavos,
    int Count);
