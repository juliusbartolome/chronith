using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.CustomerAuth.GetCustomerMe;

public sealed class GetCustomerMeQueryHandler(ICustomerRepository customerRepository)
    : IRequestHandler<GetCustomerMeQuery, CustomerDto>
{
    public async Task<CustomerDto> Handle(GetCustomerMeQuery request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        return customer.ToDto();
    }
}
