using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class BookingMapper
{
    public static BookingDto ToDto(this Booking booking) =>
        booking.ToDto(paymentUrl: null);

    public static BookingDto ToDto(this Booking booking, string? paymentUrl) =>
        new(
            Id: booking.Id,
            BookingTypeId: booking.BookingTypeId,
            Start: booking.Start,
            End: booking.End,
            Status: booking.Status,
            CustomerId: booking.CustomerId,
            CustomerEmail: booking.CustomerEmail,
            PaymentReference: booking.PaymentReference,
            AmountInCentavos: booking.AmountInCentavos,
            Currency: booking.Currency,
            CheckoutUrl: booking.CheckoutUrl,
            StaffMemberId: booking.StaffMemberId,
            StatusChanges: booking.StatusChanges
                .Select(sc => new BookingStatusChangeDto(
                    sc.Id,
                    sc.FromStatus,
                    sc.ToStatus,
                    sc.ChangedById,
                    sc.ChangedByRole,
                    sc.ChangedAt))
                .ToList(),
            PaymentUrl: paymentUrl
        );
}
