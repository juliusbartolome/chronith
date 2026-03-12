using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Bookings;

public sealed record ExportBookingsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string Format,
    string? Status = null,
    string? BookingTypeSlug = null,
    Guid? StaffMemberId = null)
    : IRequest<ExportFileResult>, IQuery;

public sealed class ExportBookingsQueryHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    ICsvExportService csvService,
    IPdfExportService pdfService)
    : IRequestHandler<ExportBookingsQuery, ExportFileResult>
{
    public async Task<ExportFileResult> Handle(ExportBookingsQuery query, CancellationToken ct)
    {
        var rows = await bookingRepo.ListForExportAsync(
            tenantContext.TenantId, query.From, query.To,
            query.Status, query.BookingTypeSlug, query.StaffMemberId, ct);

        var from = query.From.ToString("yyyyMMdd");
        var to = query.To.ToString("yyyyMMdd");

        if (query.Format == "pdf")
        {
            var pdf = pdfService.GenerateBookingsPdf(rows, string.Empty, query.From, query.To);
            return new ExportFileResult(pdf, "application/pdf", $"bookings-{from}-{to}.pdf", rows.Count);
        }

        var csv = csvService.GenerateBookingsCsv(rows);
        return new ExportFileResult(csv, "text/csv", $"bookings-{from}-{to}.csv", rows.Count);
    }
}
