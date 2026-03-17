using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PublicGetStaffAvailabilityQuery : IRequest<AvailabilityDto>, IQuery
{
    public required Guid TenantId { get; init; }
    public required Guid StaffId { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicGetStaffAvailabilityHandler(
    IStaffMemberRepository staffRepo,
    ITenantRepository tenantRepo)
    : IRequestHandler<PublicGetStaffAvailabilityQuery, AvailabilityDto>
{
    public async Task<AvailabilityDto> Handle(
        PublicGetStaffAvailabilityQuery query, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(query.TenantId, query.StaffId, ct)
            ?? throw new NotFoundException("StaffMember", query.StaffId);

        if (!staff.IsActive)
            return new AvailabilityDto([]);

        var tenant = await tenantRepo.GetByIdAsync(query.TenantId, ct)
            ?? throw new NotFoundException("Tenant", query.TenantId);

        var tz = tenant.GetTimeZone();
        var slots = GenerateAvailabilitySlots(staff, tz, query.From, query.To);

        return new AvailabilityDto(
            slots.Select(s => new AvailableSlotDto(s.Start, s.End)).ToList());
    }

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
