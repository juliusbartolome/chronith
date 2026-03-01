namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class CalendarBookingType : BookingType
{
    public IReadOnlyList<DayOfWeek> AvailableDays { get; private set; }
        = Array.Empty<DayOfWeek>();

    // For Infrastructure hydration
    internal CalendarBookingType() { }

    public static CalendarBookingType Create(
        Guid tenantId,
        string slug,
        string name,
        int capacity,
        PaymentMode paymentMode,
        string? paymentProvider,
        IReadOnlyList<DayOfWeek> availableDays)
    {
        return new CalendarBookingType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = name,
            Capacity = capacity,
            PaymentMode = paymentMode,
            PaymentProvider = paymentProvider,
            AvailableDays = availableDays
        };
    }

    public override void Update(
        string name,
        int capacity,
        PaymentMode paymentMode,
        string? paymentProvider,
        int durationMinutes,
        int bufferBeforeMinutes,
        int bufferAfterMinutes,
        IReadOnlyList<TimeSlotWindow>? availabilityWindows,
        IReadOnlyList<DayOfWeek>? availableDays)
    {
        base.Update(name, capacity, paymentMode, paymentProvider,
            durationMinutes, bufferBeforeMinutes, bufferAfterMinutes,
            availabilityWindows, availableDays);
        AvailableDays = availableDays ?? Array.Empty<DayOfWeek>();
    }

    public override (DateTimeOffset Start, DateTimeOffset End) ResolveSlot(
        DateTimeOffset requestedStart, TenantTimeZone tz)
    {
        var localDate = tz.ToLocalDate(requestedStart);
        if (!AvailableDays.Contains(localDate.DayOfWeek))
            throw new SlotNotInWindowException(requestedStart);

        var dayStart = tz.ToUtc(localDate, TimeOnly.MinValue);
        var dayEnd   = tz.ToUtc(localDate.AddDays(1), TimeOnly.MinValue);
        return (dayStart, dayEnd);
    }

    public override (DateTimeOffset EffectiveStart, DateTimeOffset EffectiveEnd)
        GetEffectiveRange(DateTimeOffset start, DateTimeOffset end)
        => (start, end); // No buffers
}
