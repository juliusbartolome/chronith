using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Bookings;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetBookingQuery(string BookingTypeSlug, Guid BookingId)
    : IRequest<BookingDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetBookingHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo)
    : IRequestHandler<GetBookingQuery, BookingDto>
{
    public async Task<BookingDto> Handle(GetBookingQuery query, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, query.BookingId, ct)
            ?? throw new NotFoundException("Booking", query.BookingId);
        return booking.ToDto();
    }
}
