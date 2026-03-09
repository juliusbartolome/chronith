using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerBookings;

public sealed class GetCustomerBookingsQueryHandler(
    ICustomerRepository customerRepository,
    IBookingRepository bookingRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetCustomerBookingsQuery, IReadOnlyList<BookingDto>>
{
    public async Task<IReadOnlyList<BookingDto>> Handle(
        GetCustomerBookingsQuery request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var bookings = await bookingRepository.GetByCustomerIdAsync(
            tenantContext.TenantId, customer.Id.ToString(), cancellationToken);

        return bookings.Select(b => b.ToDto()).ToList();
    }
}
