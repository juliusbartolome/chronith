using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.UpdateProfile;

public sealed class UpdateCustomerProfileCommandHandler(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCustomerProfileCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(
        UpdateCustomerProfileCommand request, CancellationToken cancellationToken)
    {
        var customer = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.CustomerId);

        customer.UpdateProfile(request.Name, request.Phone);
        customerRepository.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }
}
