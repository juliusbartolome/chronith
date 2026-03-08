using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record PublicGetBookingTypeQuery(Guid TenantId, string Slug)
    : IRequest<BookingTypeDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicGetBookingTypeHandler(IBookingTypeRepository repository)
    : IRequestHandler<PublicGetBookingTypeQuery, BookingTypeDto>
{
    public async Task<BookingTypeDto> Handle(PublicGetBookingTypeQuery query, CancellationToken ct)
    {
        var bt = await repository.GetBySlugAsync(query.TenantId, query.Slug, ct)
            ?? throw new NotFoundException("BookingType", query.Slug);
        return bt.ToDto();
    }
}
