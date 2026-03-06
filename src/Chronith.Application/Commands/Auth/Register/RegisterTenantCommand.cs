using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.Auth.Register;

public sealed record RegisterTenantCommand(
    string TenantName,
    string TenantSlug,
    string TimeZoneId,
    string Email,
    string Password) : IRequest<AuthTokenDto>;
