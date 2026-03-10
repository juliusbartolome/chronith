using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.Auth.Register;

public sealed record RegisterTenantCommand(
    string TenantName,
    string TenantSlug,
    string TimeZoneId,
    string Email,
    string Password) : IRequest<AuthTokenDto>, IAuditable
{
    public Guid EntityId => Guid.Empty;
    public string EntityType => "Tenant";
    public string Action => "Create";
}
