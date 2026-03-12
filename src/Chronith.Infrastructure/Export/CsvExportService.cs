using System.Globalization;
using System.Text;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;

namespace Chronith.Infrastructure.Export;

public sealed class CsvExportService : ICsvExportService
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        NewLine = "\r\n"
    };

    public byte[] GenerateBookingsCsv(IReadOnlyList<BookingExportRowDto> rows)
        => Serialize(rows);

    public byte[] GenerateAnalyticsCsv(IReadOnlyList<AnalyticsExportRowDto> rows)
        => Serialize(rows);

    public byte[] GenerateAuditCsv(IReadOnlyList<AuditExportRowDto> rows)
        => Serialize(rows);

    private static byte[] Serialize<T>(IReadOnlyList<T> rows)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        using var csv = new CsvWriter(writer, Config);
        csv.WriteRecords(rows);
        writer.Flush();
        return ms.ToArray();
    }
}
