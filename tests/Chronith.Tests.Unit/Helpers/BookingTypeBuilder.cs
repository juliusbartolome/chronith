using System.Reflection;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Tests.Unit.Helpers;

/// <summary>
/// Builder for TimeSlotBookingType and CalendarBookingType using reflection
/// to set private backing fields, since internal constructors are accessible
/// via InternalsVisibleTo but private setters are not.
/// </summary>
public static class BookingTypeBuilder
{
    public static TimeSlotBookingType BuildTimeSlot(
        int durationMinutes = 60,
        int bufferBeforeMinutes = 0,
        int bufferAfterMinutes = 0,
        IReadOnlyList<TimeSlotWindow>? windows = null,
        Guid? tenantId = null,
        long priceInCentavos = 0,
        string currency = "PHP",
        PaymentMode paymentMode = PaymentMode.Manual,
        string? paymentProvider = null,
        bool requiresStaffAssignment = false)
    {
        var bt = new TimeSlotBookingType();
        Set(bt, "Id", Guid.NewGuid());
        Set(bt, "TenantId", tenantId ?? Guid.NewGuid());
        Set(bt, "Slug", "test-slot");
        Set(bt, "Name", "Test Slot Booking");
        Set(bt, "Capacity", 1);
        Set(bt, "PaymentMode", paymentMode);
        Set(bt, "PaymentProvider", paymentProvider);
        Set(bt, "DurationMinutes", durationMinutes);
        Set(bt, "BufferBeforeMinutes", bufferBeforeMinutes);
        Set(bt, "BufferAfterMinutes", bufferAfterMinutes);
        Set(bt, "AvailabilityWindows", (IReadOnlyList<TimeSlotWindow>)(windows ?? Array.Empty<TimeSlotWindow>()));
        Set(bt, "PriceInCentavos", priceInCentavos);
        Set(bt, "Currency", currency);
        Set(bt, "RequiresStaffAssignment", requiresStaffAssignment);
        return bt;
    }

    public static CalendarBookingType BuildCalendar(
        IReadOnlyList<DayOfWeek>? availableDays = null,
        Guid? tenantId = null,
        long priceInCentavos = 0,
        string currency = "PHP")
    {
        var bt = new CalendarBookingType();
        Set(bt, "Id", Guid.NewGuid());
        Set(bt, "TenantId", tenantId ?? Guid.NewGuid());
        Set(bt, "Slug", "test-calendar");
        Set(bt, "Name", "Test Calendar Booking");
        Set(bt, "Capacity", 1);
        Set(bt, "PaymentMode", PaymentMode.Manual);
        Set(bt, "AvailableDays", (IReadOnlyList<DayOfWeek>)(availableDays ?? Array.Empty<DayOfWeek>()));
        Set(bt, "PriceInCentavos", priceInCentavos);
        Set(bt, "Currency", currency);
        return bt;
    }

    private static void Set(object obj, string propertyName, object? value)
    {
        var type = obj.GetType();
        // Walk up the hierarchy to find the property (may be on abstract base)
        while (type != null)
        {
            var prop = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
                return;
            }

            // Try backing field for auto-properties with private setter
            var field = type.GetField($"<{propertyName}>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }

            type = type.BaseType;
        }
        throw new InvalidOperationException($"Could not find property or backing field for '{propertyName}' on {obj.GetType().Name}");
    }
}
