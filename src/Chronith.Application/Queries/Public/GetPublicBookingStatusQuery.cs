using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetPublicBookingStatusQuery(Guid TenantId, Guid BookingId)
    : IRequest<PublicBookingStatusDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetPublicBookingStatusQueryHandler(
    IBookingRepository bookingRepository,
    IBookingTypeRepository bookingTypeRepository,
    ITenantPaymentConfigRepository tenantPaymentConfigRepository)
    : IRequestHandler<GetPublicBookingStatusQuery, PublicBookingStatusDto>
{
    public async Task<PublicBookingStatusDto> Handle(
        GetPublicBookingStatusQuery query, CancellationToken ct)
    {
        var booking = await bookingRepository.GetPublicByIdAsync(query.TenantId, query.BookingId, ct)
            ?? throw new NotFoundException("Booking", query.BookingId);

        return await PublicBookingStatusMapper.ToPublicStatusDtoAsync(
            booking, bookingTypeRepository, tenantPaymentConfigRepository, query.TenantId, ct);
    }
}
