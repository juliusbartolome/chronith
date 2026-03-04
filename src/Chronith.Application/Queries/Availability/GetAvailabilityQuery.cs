using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
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
    ISlotGeneratorService slotGenerator,
    IRedisCacheService? cacheService = null)
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

        // 4. Generate available slots in C# (no generate_series, fully portable)
        var slots = slotGenerator.GenerateAvailableSlots(
            bookingType, tz, query.From, query.To, bookedSlots);

        return new AvailabilityDto(
            slots.Select(s => new AvailableSlotDto(s.Start, s.End)).ToList());
    }
}
