namespace Chronith.Client.Models;

public sealed record BookingTypeDto(
    Guid Id,
    string Slug,
    string Name,
    string Kind,
    int Capacity,
    string PaymentMode,
    string? PaymentProvider,
    long PriceInCentavos,
    string Currency,
    // TimeSlot fields (null for Calendar)
    int? DurationMinutes,
    int? BufferBeforeMinutes,
    int? BufferAfterMinutes,
    IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows,
    // Calendar fields (null for TimeSlot)
    IReadOnlyList<string>? AvailableDays,
    bool RequiresStaffAssignment,
    string? CustomFieldSchema,
    string? ReminderIntervals,
    string? CustomerCallbackUrl
);

public sealed record TimeSlotWindowDto(
    string DayOfWeek,
    string StartTime,
    string EndTime
);
