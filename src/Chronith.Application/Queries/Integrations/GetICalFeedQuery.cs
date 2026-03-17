using System.Text;
using Chronith.Application.Behaviors;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Integrations;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetICalFeedQuery(string Slug) : IRequest<string>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetICalFeedHandler(
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo)
    : IRequestHandler<GetICalFeedQuery, string>
{
    public async Task<string> Handle(GetICalFeedQuery query, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(query.Slug, ct)
            ?? throw new NotFoundException("BookingType", query.Slug);

        var entries = await bookingRepo.GetICalEntriesAsync(bookingType.Id, ct);

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Chronith//Booking Engine//EN");

        foreach (var (id, start, end) in entries)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"DTSTART:{start.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"DTEND:{end.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}");
            sb.AppendLine($"SUMMARY:Booking - {bookingType.Name}");
            sb.AppendLine($"UID:{id}@chronith");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }
}
