using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.BookingTypes;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetBookingTypeQuery(string Slug) : IRequest<BookingTypeDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetBookingTypeHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository repository)
    : IRequestHandler<GetBookingTypeQuery, BookingTypeDto>
{
    public async Task<BookingTypeDto> Handle(GetBookingTypeQuery query, CancellationToken ct)
    {
        var bt = await repository.GetBySlugAsync(tenantContext.TenantId, query.Slug, ct)
            ?? throw new NotFoundException("BookingType", query.Slug);
        return bt.ToDto();
    }
}
