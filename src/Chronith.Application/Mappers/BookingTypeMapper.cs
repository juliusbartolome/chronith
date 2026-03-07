using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class BookingTypeMapper
{
    public static BookingTypeDto ToDto(this BookingType bt) =>
        bt switch
        {
            TimeSlotBookingType ts => ts.ToDto(),
            CalendarBookingType cal => cal.ToDto(),
            _ => throw new ArgumentOutOfRangeException(nameof(bt))
        };

    private static BookingTypeDto ToDto(this TimeSlotBookingType ts) =>
        new(
            Id: ts.Id,
            Slug: ts.Slug,
            Name: ts.Name,
            Kind: BookingKind.TimeSlot,
            Capacity: ts.Capacity,
            PaymentMode: ts.PaymentMode,
            PaymentProvider: ts.PaymentProvider,
            PriceInCentavos: ts.PriceInCentavos,
            Currency: ts.Currency,
            DurationMinutes: ts.DurationMinutes,
            BufferBeforeMinutes: ts.BufferBeforeMinutes,
            BufferAfterMinutes: ts.BufferAfterMinutes,
            AvailabilityWindows: ts.AvailabilityWindows
                .Select(w => new TimeSlotWindowDto(w.DayOfWeek, w.StartTime, w.EndTime))
                .ToList(),
            AvailableDays: null,
            RequiresStaffAssignment: ts.RequiresStaffAssignment,
            CustomerCallbackUrl: ts.CustomerCallbackUrl
        );

    private static BookingTypeDto ToDto(this CalendarBookingType cal) =>
        new(
            Id: cal.Id,
            Slug: cal.Slug,
            Name: cal.Name,
            Kind: BookingKind.Calendar,
            Capacity: cal.Capacity,
            PaymentMode: cal.PaymentMode,
            PaymentProvider: cal.PaymentProvider,
            PriceInCentavos: cal.PriceInCentavos,
            Currency: cal.Currency,
            DurationMinutes: null,
            BufferBeforeMinutes: null,
            BufferAfterMinutes: null,
            AvailabilityWindows: null,
            AvailableDays: cal.AvailableDays.ToList(),
            RequiresStaffAssignment: cal.RequiresStaffAssignment,
            CustomerCallbackUrl: cal.CustomerCallbackUrl
        );
}
