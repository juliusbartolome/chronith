using Chronith.Application.DTOs;

namespace Chronith.Application.Interfaces;

public interface IPdfExportService
{
    byte[] GenerateBookingsPdf(
        IReadOnlyList<BookingExportRowDto> rows,
        string tenantName,
        DateTimeOffset from,
        DateTimeOffset to);

    byte[] GenerateAnalyticsPdf(
        IReadOnlyList<AnalyticsExportRowDto> rows,
        string tenantName,
        DateTimeOffset from,
        DateTimeOffset to);
}
