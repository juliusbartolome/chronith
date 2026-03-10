using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.UpdateProfile;

public sealed record UpdateCustomerProfileCommand : IRequest<CustomerDto>, IAuditable
{
    public required Guid CustomerId { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }

    public Guid EntityId => CustomerId;
    public string EntityType => "Customer";
    public string Action => "Update";
}
