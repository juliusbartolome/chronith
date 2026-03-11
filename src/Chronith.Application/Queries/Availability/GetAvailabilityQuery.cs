using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Telemetry;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Availability;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetAvailabilityQuery : IRequest<AvailabilityDto>, IQuery
{
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetAvailabilityHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITimeBlockRepository timeBlockRepo,
    ISlotGeneratorService slotGenerator,
    IStaffMemberRepository? staffRepo = null,
    IRedisCacheService? cacheService = null,
    IBookingMetrics? metricsService = null)
    : IRequestHandler<GetAvailabilityQuery, AvailabilityDto>
{
    private static readonly BookingStatus[] ConflictStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingVerification,
        BookingStatus.Confirmed
    ];

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<AvailabilityDto> Handle(GetAvailabilityQuery query, CancellationToken ct)
    {
        if (cacheService is not null)
        {
            var cacheKey = $"avail:{tenantContext.TenantId}:{query.BookingTypeSlug}:{query.From:yyyyMMddHHmm}:{query.To:yyyyMMddHHmm}";
            return (await cacheService.GetOrSetAsync<AvailabilityDto>(
                cacheKey,
                () => FetchAvailabilityInternalAsync(query, ct),
                CacheTtl,
                ct))!;
        }

        return await FetchAvailabilityInternalAsync(query, ct);
    }

    private async Task<AvailabilityDto> FetchAvailabilityInternalAsync(
        GetAvailabilityQuery query, CancellationToken ct)
    {
        using var activity = ChronithActivitySource.StartAvailabilityCompute(tenantContext.TenantId, query.BookingTypeSlug);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Load BookingType (AsNoTracking)
        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, query.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", query.BookingTypeSlug);

        // 2. Load tenant timezone
        var tenant = await tenantRepo.GetByIdAsync(tenantContext.TenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantContext.TenantId);

        var tz = tenant.GetTimeZone();

        // 3. Load booked slots in range — one DB round-trip, projected to Start+End only
        var bookedSlots = await bookingRepo.GetBookedSlotsAsync(
            bookingType.Id, query.From, query.To, ConflictStatuses, ct);

        // 4. Load time blocks that overlap the range
        var timeBlocks = await timeBlockRepo.ListInRangeAsync(
            tenantContext.TenantId, bookingType.Id, staffMemberId: null,
            query.From, query.To, ct);

        // 5. Generate available slots in C# (no generate_series, fully portable)
        var slots = slotGenerator.GenerateAvailableSlots(
            bookingType, tz, query.From, query.To, bookedSlots);

        // 6. Filter out slots that overlap with time blocks
        var filtered = slots
            .Where(s => !timeBlocks.Any(tb => s.Start < tb.End && s.End > tb.Start))
            .ToList();

        // 7. If RequiresStaffAssignment, filter to only slots covered by at least one active staff member
        if (bookingType.RequiresStaffAssignment && staffRepo is not null)
        {
            var assignedStaff = await staffRepo.ListByBookingTypeAsync(
                tenantContext.TenantId, bookingType.Id, ct);

            filtered = FilterByStaffAvailability(filtered, assignedStaff, tz);
        }

        sw.Stop();
        metricsService?.RecordAvailabilityDuration(tenantContext.TenantId.ToString(), sw.Elapsed.TotalMilliseconds);

        return new AvailabilityDto(
            filtered.Select(s => new AvailableSlotDto(s.Start, s.End)).ToList());
    }
    /// <summary>
    /// Filters slots to only those where at least one active staff member has an
    /// availability window covering the entire slot duration.
    /// </summary>
    internal static List<(DateTimeOffset Start, DateTimeOffset End)> FilterByStaffAvailability(
        List<(DateTimeOffset Start, DateTimeOffset End)> slots,
        IReadOnlyList<StaffMember> staff,
        TenantTimeZone tz)
    {
        var activeStaff = staff.Where(s => s.IsActive).ToList();
        if (activeStaff.Count == 0) return [];

        return slots.Where(slot => activeStaff.Any(s => StaffCoversSlot(s, slot, tz))).ToList();
    }

    /// <summary>
    /// Checks whether a staff member has an availability window on the same day-of-week
    /// that fully contains the slot's time range.
    /// </summary>
    private static bool StaffCoversSlot(
        StaffMember staff,
        (DateTimeOffset Start, DateTimeOffset End) slot,
        TenantTimeZone tz)
    {
        var slotLocalStart = tz.ToLocalDateTime(slot.Start);
        var slotLocalEnd = tz.ToLocalDateTime(slot.End);
        var slotDow = slotLocalStart.DayOfWeek;
        var slotStartTime = TimeOnly.FromTimeSpan(slotLocalStart.TimeOfDay);
        var slotEndTime = TimeOnly.FromTimeSpan(slotLocalEnd.TimeOfDay);

        return staff.AvailabilityWindows.Any(w =>
            w.DayOfWeek == slotDow &&
            w.StartTime <= slotStartTime &&
            w.EndTime >= slotEndTime);
    }
}
