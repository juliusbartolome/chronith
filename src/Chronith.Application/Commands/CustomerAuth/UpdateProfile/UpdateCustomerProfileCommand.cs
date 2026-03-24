using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.UpdateProfile;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateCustomerProfileCommand : IRequest<CustomerDto>, IAuditable
{
    public required Guid CustomerId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Mobile { get; init; }

    public Guid EntityId => CustomerId;
    public string EntityType => "Customer";
    public string Action => "Update";
}

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class UpdateCustomerProfileCommandValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

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

        customer.UpdateProfile(request.FirstName, request.LastName, request.Mobile);
        customerRepository.Update(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }
}
