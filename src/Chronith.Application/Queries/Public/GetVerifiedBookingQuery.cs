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

public sealed class GetVerifiedBookingQueryHandler(
    IBookingRepository bookingRepository,
    IBookingTypeRepository bookingTypeRepository,
    ITenantPaymentConfigRepository tenantPaymentConfigRepository)
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

        // Load the booking type to determine payment mode
        var bookingType = await bookingTypeRepository.GetByIdAsync(booking.BookingTypeId, ct);
        var paymentMode = bookingType?.PaymentMode;

        // Build manual payment options when payment mode is Manual
        ManualPaymentOptionsDto? manualPaymentOptions = null;
        if (paymentMode == PaymentMode.Manual)
        {
            var config = await tenantPaymentConfigRepository
                .GetActiveByProviderNameAsync(query.TenantId, "Manual", ct);

            if (config is not null)
            {
                manualPaymentOptions = new ManualPaymentOptionsDto(
                    QrCodeUrl: config.QrCodeUrl,
                    PublicNote: config.PublicNote,
                    Label: config.Label);
            }
        }

        return new PublicBookingStatusDto(
            Id: booking.Id,
            ReferenceId: booking.Id.ToString("N"),
            Status: booking.Status,
            Start: booking.Start,
            End: booking.End,
            AmountInCentavos: booking.AmountInCentavos,
            Currency: booking.Currency,
            PaymentReference: booking.PaymentReference,
            CheckoutUrl: checkoutUrl,
            PaymentMode: paymentMode?.ToString(),
            ManualPaymentOptions: manualPaymentOptions,
            ProofOfPaymentUrl: booking.ProofOfPaymentUrl,
            ProofOfPaymentFileName: booking.ProofOfPaymentFileName,
            PaymentNote: booking.PaymentNote);
    }
}
