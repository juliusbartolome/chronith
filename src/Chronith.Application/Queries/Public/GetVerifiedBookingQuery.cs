using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetVerifiedBookingQuery(Guid TenantId, Guid BookingId)
    : IRequest<PublicBookingStatusDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetVerifiedBookingQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<GetVerifiedBookingQuery, PublicBookingStatusDto>
{
    public async Task<PublicBookingStatusDto> Handle(
        GetVerifiedBookingQuery query, CancellationToken ct)
    {
        var booking = await bookingRepository.GetPublicByIdAsync(query.TenantId, query.BookingId, ct)
            ?? throw new NotFoundException("Booking", query.BookingId);

        var checkoutUrl = booking.Status == BookingStatus.PendingPayment
            ? booking.CheckoutUrl
            : null;

        return new PublicBookingStatusDto(
            Id: booking.Id,
            Status: booking.Status,
            Start: booking.Start,
            End: booking.End,
            AmountInCentavos: booking.AmountInCentavos,
            Currency: booking.Currency,
            PaymentReference: booking.PaymentReference,
            CheckoutUrl: checkoutUrl);
    }
}
