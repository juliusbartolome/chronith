using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.UpdateProfile;

public sealed record UpdateCustomerProfileCommand : IRequest<CustomerDto>
{
    public required Guid CustomerId { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
}
