using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.Auth.Login;

public sealed record LoginCommand(
    string TenantSlug,
    string Email,
    string Password) : IRequest<AuthTokenDto>;
