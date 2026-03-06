using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record BookingTypeDto(
    Guid Id,
    string Slug,
    string Name,
    BookingKind Kind,
    int Capacity,
    PaymentMode PaymentMode,
    string? PaymentProvider,
    long PriceInCentavos,
    string Currency,
    // TimeSlot fields (null for Calendar)
    int? DurationMinutes,
    int? BufferBeforeMinutes,
    int? BufferAfterMinutes,
    IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows,
    // Calendar fields (null for TimeSlot)
    IReadOnlyList<DayOfWeek>? AvailableDays
);

public sealed record TimeSlotWindowDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);
