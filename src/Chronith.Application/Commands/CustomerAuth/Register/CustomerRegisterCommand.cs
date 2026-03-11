using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Register;

public sealed record CustomerRegisterCommand : IRequest<CustomerAuthTokenDto>, IAuditable
{
    public required string TenantSlug { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "Customer";
    public string Action => "Create";
}
