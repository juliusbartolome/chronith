using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Waitlist;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListWaitlistQuery(
    string BookingTypeSlug,
    DateTimeOffset From,
    DateTimeOffset To)
    : IRequest<IReadOnlyList<WaitlistEntryDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListWaitlistHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IWaitlistRepository waitlistRepo)
    : IRequestHandler<ListWaitlistQuery, IReadOnlyList<WaitlistEntryDto>>
{
    public async Task<IReadOnlyList<WaitlistEntryDto>> Handle(
        ListWaitlistQuery query, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, query.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", query.BookingTypeSlug);

        var entries = await waitlistRepo.ListBySlotAsync(
            tenantContext.TenantId, bookingType.Id, query.From, query.To, ct);

        return entries.Select(e => e.ToDto()).ToList();
    }
}
