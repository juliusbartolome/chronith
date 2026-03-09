using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantAuthConfigMapper
{
    public static TenantAuthConfigDto ToDto(this TenantAuthConfig config) =>
        new(config.Id, config.AllowBuiltInAuth, config.OidcIssuer, config.OidcClientId,
            config.OidcAudience, config.MagicLinkEnabled);
}
