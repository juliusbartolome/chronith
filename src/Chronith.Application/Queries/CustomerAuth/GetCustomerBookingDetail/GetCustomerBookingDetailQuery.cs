using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerBookingDetail;

public sealed record GetCustomerBookingDetailQuery(Guid CustomerId, Guid BookingId)
    : IRequest<BookingDto>, IQuery;
