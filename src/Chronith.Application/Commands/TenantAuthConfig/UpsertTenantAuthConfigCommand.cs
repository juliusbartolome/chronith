using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.TenantAuthConfig;

public sealed record UpsertTenantAuthConfigCommand : IRequest<TenantAuthConfigDto>, IAuditable
{
    public required bool AllowBuiltInAuth { get; init; }
    public string? OidcIssuer { get; init; }
    public string? OidcClientId { get; init; }
    public string? OidcAudience { get; init; }
    public required bool MagicLinkEnabled { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "Tenant";
    public string Action => "UpsertAuthConfig";
}
