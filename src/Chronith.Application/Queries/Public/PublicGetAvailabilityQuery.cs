using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Availability;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PublicGetAvailabilityQuery : IRequest<AvailabilityDto>, IQuery
{
    public required Guid TenantId { get; init; }
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicGetAvailabilityHandler(
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITimeBlockRepository timeBlockRepo,
    ISlotGeneratorService slotGenerator,
    IStaffMemberRepository? staffRepo = null)
    : IRequestHandler<PublicGetAvailabilityQuery, AvailabilityDto>
{
    private static readonly BookingStatus[] ConflictStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingVerification,
        BookingStatus.Confirmed
    ];

    public async Task<AvailabilityDto> Handle(PublicGetAvailabilityQuery query, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(query.TenantId, query.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", query.BookingTypeSlug);

        var tenant = await tenantRepo.GetByIdAsync(query.TenantId, ct)
            ?? throw new NotFoundException("Tenant", query.TenantId);

        var tz = tenant.GetTimeZone();

        var bookedSlots = await bookingRepo.GetBookedSlotsAsync(
            bookingType.Id, query.From, query.To, ConflictStatuses, ct);

        var timeBlocks = await timeBlockRepo.ListInRangeAsync(
            query.TenantId, bookingType.Id, staffMemberId: null,
            query.From, query.To, ct);

        var slots = slotGenerator.GenerateAvailableSlots(
            bookingType, tz, query.From, query.To, bookedSlots);

        var filtered = slots
            .Where(s => !timeBlocks.Any(tb => s.Start < tb.End && s.End > tb.Start))
            .ToList();

        // Filter by staff availability when booking type requires staff assignment
        if (bookingType.RequiresStaffAssignment && staffRepo is not null)
        {
            var assignedStaff = await staffRepo.ListByBookingTypeAsync(
                query.TenantId, bookingType.Id, ct);

            filtered = GetAvailabilityHandler.FilterByStaffAvailability(filtered, assignedStaff, tz);
        }

        return new AvailabilityDto(
            filtered.Select(s => new AvailableSlotDto(s.Start, s.End)).ToList());
    }
}
