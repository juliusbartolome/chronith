using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerBookingDetail;

public sealed class GetCustomerBookingDetailQueryHandler(
    ICustomerRepository customerRepository,
    IBookingRepository bookingRepository,
    ITenantContext tenantContext)
    : IRequestHandler<GetCustomerBookingDetailQuery, BookingDto>
{
    public async Task<BookingDto> Handle(
        GetCustomerBookingDetailQuery request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        var booking = await bookingRepository.GetByIdAsync(
            tenantContext.TenantId, request.BookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(Booking), request.BookingId);

        // Ensure booking belongs to this customer
        if (booking.CustomerId != customer.Id.ToString())
            throw new NotFoundException(nameof(Booking), request.BookingId);

        return booking.ToDto();
    }
}
