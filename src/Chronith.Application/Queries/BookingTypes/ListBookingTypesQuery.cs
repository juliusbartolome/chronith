using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.BookingTypes;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record ListBookingTypesQuery : IRequest<IReadOnlyList<BookingTypeDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ListBookingTypesHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository repository)
    : IRequestHandler<ListBookingTypesQuery, IReadOnlyList<BookingTypeDto>>
{
    public async Task<IReadOnlyList<BookingTypeDto>> Handle(
        ListBookingTypesQuery query, CancellationToken ct)
    {
        var types = await repository.ListAsync(tenantContext.TenantId, ct);
        return types.Select(bt => bt.ToDto()).ToList();
    }
}
