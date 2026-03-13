namespace Chronith.Client.Models;

public sealed record AnalyticsBookingsDto(
    int TotalBookings,
    int ConfirmedBookings,
    int CancelledBookings,
    int PendingBookings,
    decimal ConfirmedRate,
    decimal CancellationRate,
    decimal AverageBookingsPerDay,
    IReadOnlyList<AnalyticsDataPointDto> BookingsOverTime
);

public sealed record AnalyticsDataPointDto(string Label, long Value);
