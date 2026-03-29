using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class PublicBookingStatusMapper
{
    public static async Task<PublicBookingStatusDto> ToPublicStatusDtoAsync(
        Booking booking,
        IBookingTypeRepository bookingTypeRepo,
        ITenantPaymentConfigRepository configRepo,
        Guid tenantId,
        CancellationToken ct)
    {
        var checkoutUrl = booking.Status == BookingStatus.PendingPayment
            ? booking.CheckoutUrl
            : null;

        // Load the booking type to determine payment mode
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);
        var paymentMode = bookingType?.PaymentMode;

        // Build manual payment options when payment mode is Manual
        ManualPaymentOptionsDto? manualPaymentOptions = null;
        if (paymentMode == PaymentMode.Manual)
        {
            var config = await configRepo
                .GetActiveByProviderNameAsync(tenantId, "Manual", ct);

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
