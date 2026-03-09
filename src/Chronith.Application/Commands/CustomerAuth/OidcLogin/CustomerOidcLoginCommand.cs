using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.OidcLogin;

public sealed record CustomerOidcLoginCommand(
    string TenantSlug,
    string IdToken) : IRequest<CustomerAuthTokenDto>;
