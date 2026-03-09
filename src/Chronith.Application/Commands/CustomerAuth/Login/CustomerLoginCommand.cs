using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Login;

public sealed record CustomerLoginCommand(
    string TenantSlug,
    string Email,
    string Password) : IRequest<CustomerAuthTokenDto>;
