using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerMe;

public sealed record GetCustomerMeQuery(Guid CustomerId) : IRequest<CustomerDto>, IQuery;
