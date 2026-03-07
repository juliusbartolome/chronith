namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class TimeSlotBookingType : BookingType
{
    public int DurationMinutes { get; private set; }
    public int BufferBeforeMinutes { get; private set; }
    public int BufferAfterMinutes { get; private set; }
    public IReadOnlyList<TimeSlotWindow> AvailabilityWindows { get; private set; }
        = Array.Empty<TimeSlotWindow>();

    // For Infrastructure hydration
    internal TimeSlotBookingType() { }

    public static TimeSlotBookingType Create(
        Guid tenantId,
        string slug,
        string name,
        int capacity,
        PaymentMode paymentMode,
        string? paymentProvider,
        int durationMinutes,
        int bufferBeforeMinutes,
        int bufferAfterMinutes,
        IReadOnlyList<TimeSlotWindow> availabilityWindows,
        long priceInCentavos,
        string currency,
        bool requiresStaffAssignment = false)
    {
        return new TimeSlotBookingType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = name,
            Capacity = capacity,
            PaymentMode = paymentMode,
            PaymentProvider = paymentProvider,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = bufferBeforeMinutes,
            BufferAfterMinutes = bufferAfterMinutes,
            AvailabilityWindows = availabilityWindows,
            PriceInCentavos = priceInCentavos,
            Currency = currency,
            RequiresStaffAssignment = requiresStaffAssignment
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
        IReadOnlyList<DayOfWeek>? availableDays,
        long priceInCentavos,
        string currency,
        bool requiresStaffAssignment = false)
    {
        base.Update(name, capacity, paymentMode, paymentProvider,
            durationMinutes, bufferBeforeMinutes, bufferAfterMinutes,
            availabilityWindows, availableDays, priceInCentavos, currency,
            requiresStaffAssignment);
        DurationMinutes = durationMinutes;
        BufferBeforeMinutes = bufferBeforeMinutes;
        BufferAfterMinutes = bufferAfterMinutes;
        AvailabilityWindows = availabilityWindows ?? Array.Empty<TimeSlotWindow>();
    }

    public override (DateTimeOffset Start, DateTimeOffset End) ResolveSlot(
        DateTimeOffset requestedStart, TenantTimeZone tz)
    {
        var localDt = tz.ToLocalDateTime(requestedStart);
        var dow = localDt.DayOfWeek;
        var localTime = TimeOnly.FromTimeSpan(localDt.TimeOfDay);

        var slotEnd = localTime.AddMinutes(DurationMinutes);

        var window = AvailabilityWindows.FirstOrDefault(w =>
            w.DayOfWeek == dow &&
            localTime >= w.StartTime &&
            slotEnd <= w.EndTime);

        if (window is null)
            throw new SlotNotInWindowException(requestedStart);

        return (requestedStart, requestedStart.AddMinutes(DurationMinutes));
    }

    public override (DateTimeOffset EffectiveStart, DateTimeOffset EffectiveEnd)
        GetEffectiveRange(DateTimeOffset start, DateTimeOffset end)
        => (start.AddMinutes(-BufferBeforeMinutes), end.AddMinutes(BufferAfterMinutes));
}
