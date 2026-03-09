using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Register;

public sealed record CustomerRegisterCommand : IRequest<CustomerAuthTokenDto>
{
    public required string TenantSlug { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
}
