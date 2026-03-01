using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Bookings;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListBookingsQuery : IRequest<PagedResultDto<BookingDto>>, IQuery
{
    public required string BookingTypeSlug { get; init; }
    public required Guid BookingTypeId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListBookingsHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo)
    : IRequestHandler<ListBookingsQuery, PagedResultDto<BookingDto>>
{
    public async Task<PagedResultDto<BookingDto>> Handle(
        ListBookingsQuery query, CancellationToken ct)
    {
        // Resolve bookingTypeId from slug if not provided directly
        Guid bookingTypeId = query.BookingTypeId;
        if (bookingTypeId == Guid.Empty)
        {
            var bt = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, query.BookingTypeSlug, ct)
                ?? throw new NotFoundException("BookingType", query.BookingTypeSlug);
            bookingTypeId = bt.Id;
        }

        var (items, total) = await bookingRepo.ListAsync(
            tenantContext.TenantId, bookingTypeId, query.Page, query.PageSize, ct);

        return new PagedResultDto<BookingDto>(
            items.Select(b => b.ToDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }
}
