using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerBookings;

public sealed record GetCustomerBookingsQuery(Guid CustomerId) : IRequest<IReadOnlyList<BookingDto>>, IQuery;
