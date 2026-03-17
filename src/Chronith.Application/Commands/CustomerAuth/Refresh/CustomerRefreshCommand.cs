using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Refresh;

public sealed record CustomerRefreshCommand(
    string TenantSlug,
    string RefreshToken) : IRequest<CustomerAuthTokenDto>;
