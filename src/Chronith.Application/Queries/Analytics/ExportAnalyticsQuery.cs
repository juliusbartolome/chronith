using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Queries.Analytics;

public sealed record ExportAnalyticsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string GroupBy,
    string Format)
    : IRequest<ExportFileResult>, IQuery;

public sealed class ExportAnalyticsQueryHandler(
    ITenantContext tenantContext,
    IAnalyticsRepository analyticsRepo,
    ICsvExportService csvService,
    IPdfExportService pdfService)
    : IRequestHandler<ExportAnalyticsQuery, ExportFileResult>
{
    public async Task<ExportFileResult> Handle(ExportAnalyticsQuery query, CancellationToken ct)
    {
        var analytics = await analyticsRepo.GetBookingAnalyticsAsync(
            tenantContext.TenantId, query.From, query.To, query.GroupBy, ct);

        var rows = analytics.TimeSeries
            .Select(ts => new AnalyticsExportRowDto(ts.Date, ts.Count))
            .ToList();

        var from = query.From.ToString("yyyyMMdd");
        var to = query.To.ToString("yyyyMMdd");

        if (query.Format == "pdf")
        {
            var pdf = pdfService.GenerateAnalyticsPdf(rows, string.Empty, query.From, query.To);
            return new ExportFileResult(pdf, "application/pdf", $"analytics-{from}-{to}.pdf");
        }

        var csv = csvService.GenerateAnalyticsCsv(rows);
        return new ExportFileResult(csv, "text/csv", $"analytics-{from}-{to}.csv");
    }
}
