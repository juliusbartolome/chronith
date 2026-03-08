using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PublicListBookingTypesQuery(Guid TenantId)
    : IRequest<IReadOnlyList<BookingTypeDto>>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicListBookingTypesHandler(IBookingTypeRepository repository)
    : IRequestHandler<PublicListBookingTypesQuery, IReadOnlyList<BookingTypeDto>>
{
    public async Task<IReadOnlyList<BookingTypeDto>> Handle(
        PublicListBookingTypesQuery query, CancellationToken ct)
    {
        var types = await repository.ListAsync(query.TenantId, ct);
        return types.Select(bt => bt.ToDto()).ToList();
    }
}
