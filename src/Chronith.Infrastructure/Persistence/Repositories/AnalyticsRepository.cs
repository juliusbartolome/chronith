using Chronith.Application.DTOs;
using Chronith.Application.Extensions;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class AnalyticsRepository(ChronithDbContext db) : IAnalyticsRepository
{
    private static readonly List<BookingStatus> ConfirmedStatuses =
    [
        BookingStatus.Confirmed
    ];

    public async Task<BookingAnalyticsDto> GetBookingAnalyticsAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct)
    {
        var bookings = db.Bookings
            .TagWith("GetBookingAnalyticsAsync — AnalyticsRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.Start >= from && b.Start < to);

        var total = await bookings.CountAsync(ct);

        // By status
        var byStatusRaw = await bookings
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = byStatusRaw.ToDictionary(
            x => x.Status.ToString().ToSnakeCase(),
            x => x.Count);

        // By booking type
        var byBookingType = await bookings
            .Join(db.BookingTypes.AsNoTracking(), b => b.BookingTypeId, bt => bt.Id, (b, bt) => new { bt.Slug, bt.Name })
            .GroupBy(x => new { x.Slug, x.Name })
            .Select(g => new BookingTypeCountDto(g.Key.Slug, g.Key.Name, g.Count()))
            .ToListAsync(ct);

        // By staff (only bookings with staff assigned)
        var byStaff = await bookings
            .Where(b => b.StaffMemberId != null)
            .Join(db.StaffMembers.AsNoTracking(), b => b.StaffMemberId, s => s.Id, (b, s) => new { s.Id, s.Name })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new StaffCountDto(g.Key.Id, g.Key.Name, g.Count()))
            .ToListAsync(ct);

        // Time series
        var timeSeries = await GetBookingTimeSeries(bookings, groupBy, ct);

        var period = FormatPeriod(from, to);

        return new BookingAnalyticsDto(period, total, byStatus, byBookingType, byStaff, timeSeries);
    }

    public async Task<RevenueAnalyticsDto> GetRevenueAnalyticsAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, string groupBy, CancellationToken ct)
    {
        // Only confirmed bookings count as revenue
        var bookings = db.Bookings
            .TagWith("GetRevenueAnalyticsAsync — AnalyticsRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.Start >= from && b.Start < to
                && b.Status == BookingStatus.Confirmed);

        var totalCentavos = await bookings.SumAsync(b => b.AmountInCentavos, ct);

        // By booking type
        var byBookingType = await bookings
            .Join(db.BookingTypes.AsNoTracking(), b => b.BookingTypeId, bt => bt.Id, (b, bt) => new { bt.Slug, b.AmountInCentavos })
            .GroupBy(x => x.Slug)
            .Select(g => new BookingTypeRevenueDto(g.Key, g.Sum(x => x.AmountInCentavos), g.Count()))
            .ToListAsync(ct);

        // Time series
        var timeSeries = await GetRevenueTimeSeries(bookings, groupBy, ct);

        var period = FormatPeriod(from, to);

        return new RevenueAnalyticsDto(period, totalCentavos, "PHP", byBookingType, timeSeries);
    }

    public async Task<UtilizationAnalyticsDto> GetUtilizationAnalyticsAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var confirmedStatuses = new List<BookingStatus> { BookingStatus.Confirmed };

        // Load booking types with availability windows for slot calculation
        var bookingTypes = await db.BookingTypes
            .TagWith("GetUtilizationAnalyticsAsync.bookingTypes — AnalyticsRepository")
            .AsNoTracking()
            .Include(bt => bt.AvailabilityWindows)
            .AsSplitQuery()
            .Where(bt => bt.TenantId == tenantId && !bt.IsDeleted)
            .ToListAsync(ct);

        var bookings = await db.Bookings
            .TagWith("GetUtilizationAnalyticsAsync.bookings — AnalyticsRepository")
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.Start >= from && b.Start < to
                && confirmedStatuses.Contains(b.Status))
            .ToListAsync(ct);

        // By booking type
        var byBookingType = new List<BookingTypeUtilizationDto>();
        foreach (var bt in bookingTypes)
        {
            var totalSlots = CalculateTotalSlots(bt, from, to);
            var bookedSlots = bookings.Count(b => b.BookingTypeId == bt.Id);
            var rate = totalSlots > 0 ? Math.Round((decimal)bookedSlots / totalSlots, 2) : 0m;
            byBookingType.Add(new BookingTypeUtilizationDto(bt.Slug, totalSlots, bookedSlots, rate));
        }

        // By staff
        var staffMembers = await db.StaffMembers
            .TagWith("GetUtilizationAnalyticsAsync.staffMembers — AnalyticsRepository")
            .AsNoTracking()
            .Include(s => s.AvailabilityWindows)
            .AsSplitQuery()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive)
            .ToListAsync(ct);

        var byStaff = new List<StaffUtilizationDto>();
        foreach (var staff in staffMembers)
        {
            var totalSlots = CalculateStaffTotalSlots(staff, from, to);
            var bookedSlots = bookings.Count(b => b.StaffMemberId == staff.Id);
            var rate = totalSlots > 0 ? Math.Round((decimal)bookedSlots / totalSlots, 2) : 0m;
            byStaff.Add(new StaffUtilizationDto(staff.Id, staff.Name, totalSlots, bookedSlots, rate));
        }

        var period = FormatPeriod(from, to);

        return new UtilizationAnalyticsDto(period, byBookingType, byStaff);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<List<TimeSeriesPointDto>> GetBookingTimeSeries(
        IQueryable<Entities.BookingEntity> bookings, string groupBy, CancellationToken ct)
    {
        return groupBy switch
        {
            "week" => await bookings
                .GroupBy(b => new { b.Start.Year, Week = (b.Start.DayOfYear - 1) / 7 + 1 })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Week)
                .Select(g => new TimeSeriesPointDto(
                    $"{g.Key.Year}-W{g.Key.Week:D2}",
                    g.Count()))
                .ToListAsync(ct),

            "month" => await bookings
                .GroupBy(b => new { b.Start.Year, b.Start.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new TimeSeriesPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    g.Count()))
                .ToListAsync(ct),

            _ => await bookings // default "day"
                .GroupBy(b => new { b.Start.Year, b.Start.Month, b.Start.Day })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ThenBy(g => g.Key.Day)
                .Select(g => new TimeSeriesPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}-{g.Key.Day:D2}",
                    g.Count()))
                .ToListAsync(ct)
        };
    }

    private static async Task<List<RevenueTimeSeriesPointDto>> GetRevenueTimeSeries(
        IQueryable<Entities.BookingEntity> bookings, string groupBy, CancellationToken ct)
    {
        return groupBy switch
        {
            "week" => await bookings
                .GroupBy(b => new { b.Start.Year, Week = (b.Start.DayOfYear - 1) / 7 + 1 })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Week)
                .Select(g => new RevenueTimeSeriesPointDto(
                    $"{g.Key.Year}-W{g.Key.Week:D2}",
                    g.Sum(x => x.AmountInCentavos),
                    g.Count()))
                .ToListAsync(ct),

            "month" => await bookings
                .GroupBy(b => new { b.Start.Year, b.Start.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new RevenueTimeSeriesPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    g.Sum(x => x.AmountInCentavos),
                    g.Count()))
                .ToListAsync(ct),

            _ => await bookings
                .GroupBy(b => new { b.Start.Year, b.Start.Month, b.Start.Day })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).ThenBy(g => g.Key.Day)
                .Select(g => new RevenueTimeSeriesPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}-{g.Key.Day:D2}",
                    g.Sum(x => x.AmountInCentavos),
                    g.Count()))
                .ToListAsync(ct)
        };
    }

    /// <summary>
    /// Estimates total available slots for a booking type within the date range.
    /// Uses availability windows (day-of-week + time range) and duration to count slots.
    /// </summary>
    private static int CalculateTotalSlots(
        Entities.BookingTypeEntity bt, DateTimeOffset from, DateTimeOffset to)
    {
        if (bt.DurationMinutes is null or 0 || bt.AvailabilityWindows.Count == 0)
            return 0;

        var totalSlots = 0;
        var current = from.Date;
        var endDate = to.Date;

        while (current < endDate)
        {
            var dayOfWeek = (int)current.DayOfWeek;
            var windows = bt.AvailabilityWindows.Where(w => w.DayOfWeek == dayOfWeek);

            foreach (var window in windows)
            {
                var windowMinutes = (window.EndTime - window.StartTime).TotalMinutes;
                var effectiveDuration = bt.DurationMinutes.Value
                    + (bt.BufferBeforeMinutes ?? 0)
                    + (bt.BufferAfterMinutes ?? 0);

                if (effectiveDuration > 0)
                    totalSlots += (int)(windowMinutes / effectiveDuration) * bt.Capacity;
            }

            current = current.AddDays(1);
        }

        return totalSlots;
    }

    /// <summary>
    /// Estimates total available slots for a staff member based on availability windows.
    /// Uses 60-minute default slot duration for staff utilization.
    /// </summary>
    private static int CalculateStaffTotalSlots(
        Entities.StaffMemberEntity staff, DateTimeOffset from, DateTimeOffset to)
    {
        if (staff.AvailabilityWindows.Count == 0)
            return 0;

        const int defaultSlotMinutes = 60;
        var totalSlots = 0;
        var current = from.Date;
        var endDate = to.Date;

        while (current < endDate)
        {
            var dayOfWeek = (int)current.DayOfWeek;
            var windows = staff.AvailabilityWindows.Where(w => w.DayOfWeek == dayOfWeek);

            foreach (var window in windows)
            {
                var windowMinutes = (window.EndTime - window.StartTime).TotalMinutes;
                totalSlots += (int)(windowMinutes / defaultSlotMinutes);
            }

            current = current.AddDays(1);
        }

        return totalSlots;
    }

    private static string FormatPeriod(DateTimeOffset from, DateTimeOffset to)
        => $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";
}
