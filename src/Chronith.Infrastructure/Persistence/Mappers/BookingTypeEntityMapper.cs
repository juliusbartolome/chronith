using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between BookingTypeEntity + AvailabilityWindowEntity and BookingType domain objects.
/// Static only — no reflection.
/// </summary>
public static class BookingTypeEntityMapper
{
    public static BookingType ToDomain(BookingTypeEntity entity)
        => entity.Kind switch
        {
            BookingKind.TimeSlot => MapTimeSlot(entity),
            BookingKind.Calendar => MapCalendar(entity),
            _ => throw new ArgumentOutOfRangeException(nameof(entity.Kind), entity.Kind, null)
        };

    private static TimeSlotBookingType MapTimeSlot(BookingTypeEntity e)
    {
        var domain = new TimeSlotBookingType();
        SetBaseProperties(domain, e);
        SetProperty(domain, nameof(TimeSlotBookingType.DurationMinutes), e.DurationMinutes ?? 0);
        SetProperty(domain, nameof(TimeSlotBookingType.BufferBeforeMinutes), e.BufferBeforeMinutes ?? 0);
        SetProperty(domain, nameof(TimeSlotBookingType.BufferAfterMinutes), e.BufferAfterMinutes ?? 0);

        var windows = e.AvailabilityWindows
            .Select(w => new TimeSlotWindow(
                (DayOfWeek)w.DayOfWeek,
                w.StartTime,
                w.EndTime))
            .ToList();

        SetProperty(domain, nameof(TimeSlotBookingType.AvailabilityWindows),
            (IReadOnlyList<TimeSlotWindow>)windows);

        return domain;
    }

    private static CalendarBookingType MapCalendar(BookingTypeEntity e)
    {
        var domain = new CalendarBookingType();
        SetBaseProperties(domain, e);

        var days = string.IsNullOrWhiteSpace(e.AvailableDays)
            ? Array.Empty<DayOfWeek>()
            : e.AvailableDays.Split(',')
                .Select(d => (DayOfWeek)int.Parse(d))
                .ToArray();

        SetProperty(domain, nameof(CalendarBookingType.AvailableDays),
            (IReadOnlyList<DayOfWeek>)days);

        return domain;
    }

    private static void SetBaseProperties(BookingType domain, BookingTypeEntity e)
    {
        SetProperty(domain, nameof(BookingType.Id), e.Id);
        SetProperty(domain, nameof(BookingType.TenantId), e.TenantId);
        SetProperty(domain, nameof(BookingType.Slug), e.Slug);
        SetProperty(domain, nameof(BookingType.Name), e.Name);
        SetProperty(domain, nameof(BookingType.Capacity), e.Capacity);
        SetProperty(domain, nameof(BookingType.PaymentMode), e.PaymentMode);
        SetProperty(domain, nameof(BookingType.PaymentProvider), e.PaymentProvider);
        SetProperty(domain, nameof(BookingType.PriceInCentavos), e.PriceInCentavos);
        SetProperty(domain, nameof(BookingType.Currency), e.Currency);
        SetProperty(domain, nameof(BookingType.IsDeleted), e.IsDeleted);
        SetProperty(domain, nameof(BookingType.RequiresStaffAssignment), e.RequiresStaffAssignment);
        SetProperty(domain, nameof(BookingType.CustomFieldSchema), e.CustomFieldSchema);
        SetProperty(domain, nameof(BookingType.ReminderIntervals), e.ReminderIntervals);
        SetProperty(domain, nameof(BookingType.CustomerCallbackUrl), e.CustomerCallbackUrl);
        SetProperty(domain, nameof(BookingType.CustomerCallbackSecret), e.CustomerCallbackSecret);
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (prop is null)
        {
            // Walk up inheritance chain
            var type = target.GetType().BaseType;
            while (type is not null && prop is null)
            {
                prop = type.GetProperty(propertyName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                type = type.BaseType;
            }
        }

        prop?.SetValue(target, value);
    }

    // ── Entity → new entity (for updates) ────────────────────────────────────

    public static BookingTypeEntity ToEntity(BookingType domain)
    {
        var entity = new BookingTypeEntity
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            Slug = domain.Slug,
            Name = domain.Name,
            Capacity = domain.Capacity,
            PaymentMode = domain.PaymentMode,
            PaymentProvider = domain.PaymentProvider,
            PriceInCentavos = domain.PriceInCentavos,
            Currency = domain.Currency,
            IsDeleted = domain.IsDeleted,
            RequiresStaffAssignment = domain.RequiresStaffAssignment,
            CustomFieldSchema = domain.CustomFieldSchema,
            ReminderIntervals = domain.ReminderIntervals,
            CustomerCallbackUrl = domain.CustomerCallbackUrl,
            CustomerCallbackSecret = domain.CustomerCallbackSecret
        };

        switch (domain)
        {
            case TimeSlotBookingType ts:
                entity.Kind = BookingKind.TimeSlot;
                entity.DurationMinutes = ts.DurationMinutes;
                entity.BufferBeforeMinutes = ts.BufferBeforeMinutes;
                entity.BufferAfterMinutes = ts.BufferAfterMinutes;
                entity.AvailabilityWindows = ts.AvailabilityWindows.Select(w => new AvailabilityWindowEntity
                {
                    Id = Guid.NewGuid(),
                    BookingTypeId = domain.Id,
                    DayOfWeek = (int)w.DayOfWeek,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime
                }).ToList();
                break;

            case CalendarBookingType cal:
                entity.Kind = BookingKind.Calendar;
                entity.AvailableDays = cal.AvailableDays.Count > 0
                    ? string.Join(',', cal.AvailableDays.Select(d => (int)d))
                    : null;
                break;
        }

        return entity;
    }
}
