using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Staff;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetStaffAvailabilityQuery : IRequest<AvailabilityDto>, IQuery
{
    public required Guid StaffId { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetStaffAvailabilityHandler(
    ITenantContext tenantContext,
    IStaffMemberRepository staffRepo,
    ITenantRepository tenantRepo)
    : IRequestHandler<GetStaffAvailabilityQuery, AvailabilityDto>
{
    public async Task<AvailabilityDto> Handle(
        GetStaffAvailabilityQuery query, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, query.StaffId, ct)
            ?? throw new NotFoundException("StaffMember", query.StaffId);

        if (!staff.IsActive)
            return new AvailabilityDto([]);

        var tenant = await tenantRepo.GetByIdAsync(tenantContext.TenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantContext.TenantId);

        var tz = tenant.GetTimeZone();
        var slots = GenerateAvailabilitySlots(staff, tz, query.From, query.To);

        return new AvailabilityDto(
            slots.Select(s => new AvailableSlotDto(s.Start, s.End)).ToList());
    }

    /// <summary>
    /// Projects staff availability windows onto the requested date range.
    /// For each day in range, if the staff has a window matching that day-of-week,
    /// a slot is emitted with the appropriate start/end in UTC.
    /// </summary>
    private static List<(DateTimeOffset Start, DateTimeOffset End)> GenerateAvailabilitySlots(
        StaffMember staff,
        TenantTimeZone tz,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var slots = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var windowsByDay = staff.AvailabilityWindows
            .GroupBy(w => w.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Walk each day in the range (in the tenant's timezone)
        var startDate = tz.ToLocalDate(from);
        var endDate = tz.ToLocalDate(to);
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            if (windowsByDay.TryGetValue(currentDate.DayOfWeek, out var windows))
            {
                foreach (var window in windows)
                {
                    var slotUtcStart = tz.ToUtc(currentDate, window.StartTime);
                    var slotUtcEnd = tz.ToUtc(currentDate, window.EndTime);

                    // Only include slots that overlap the requested range
                    if (slotUtcEnd > from && slotUtcStart < to)
                    {
                        slots.Add((slotUtcStart, slotUtcEnd));
                    }
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return slots.OrderBy(s => s.Start).ToList();
    }
}
