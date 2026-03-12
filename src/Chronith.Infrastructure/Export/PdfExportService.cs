using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Chronith.Infrastructure.Export;

public sealed class PdfExportService : IPdfExportService
{
    public byte[] GenerateBookingsPdf(
        IReadOnlyList<BookingExportRowDto> rows,
        string tenantName,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text($"Bookings Export — {tenantName}").FontSize(16).Bold();
                    col.Item().Text($"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}  •  Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(80);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Grey.Lighten2).Padding(4);

                    table.Header(header =>
                    {
                        foreach (var h in new[] { "ID", "Booking Type", "Customer", "Start", "Status", "Amount (PHP)" })
                            header.Cell().Element(HeaderCell).Text(h).Bold().FontSize(8);
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Padding(4).Text(row.Id.ToString()[..8]).FontSize(8);
                        table.Cell().Padding(4).Text(row.BookingTypeName).FontSize(8);
                        table.Cell().Padding(4).Text(row.CustomerEmail).FontSize(8);
                        table.Cell().Padding(4).Text(row.Start.ToString("yyyy-MM-dd HH:mm")).FontSize(8);
                        table.Cell().Padding(4).Text(row.Status).FontSize(8);
                        table.Cell().Padding(4).Text($"₱{row.AmountInCentavos / 100m:F2}").FontSize(8);
                    }
                });

                page.Footer().AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
            });
        });

        return doc.GeneratePdf();
    }

    public byte[] GenerateAnalyticsPdf(
        IReadOnlyList<AnalyticsExportRowDto> rows,
        string tenantName,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);

                page.Header().Column(col =>
                {
                    col.Item().Text($"Analytics Export — {tenantName}").FontSize(16).Bold();
                    col.Item().Text($"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}  •  Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1);
                    });

                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Grey.Lighten2).Padding(4);

                    table.Header(header =>
                    {
                        foreach (var h in new[] { "Date", "Total Bookings" })
                            header.Cell().Element(HeaderCell).Text(h).Bold().FontSize(8);
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Padding(4).Text(row.Date).FontSize(8);
                        table.Cell().Padding(4).Text(row.TotalBookings.ToString()).FontSize(8);
                    }
                });
            });
        });

        return doc.GeneratePdf();
    }
}
