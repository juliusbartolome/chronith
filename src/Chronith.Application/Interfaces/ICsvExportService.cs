using Chronith.Application.DTOs;

namespace Chronith.Application.Interfaces;

public interface ICsvExportService
{
    byte[] GenerateBookingsCsv(IReadOnlyList<BookingExportRowDto> rows);
    byte[] GenerateAnalyticsCsv(IReadOnlyList<AnalyticsExportRowDto> rows);
    byte[] GenerateAuditCsv(IReadOnlyList<AuditExportRowDto> rows);
}
