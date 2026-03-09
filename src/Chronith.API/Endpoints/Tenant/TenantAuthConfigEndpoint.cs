using Chronith.Application.Commands.TenantAuthConfig;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class UpsertTenantAuthConfigRequest
{
    public bool AllowBuiltInAuth { get; set; }
    public string? OidcIssuer { get; set; }
    public string? OidcClientId { get; set; }
    public string? OidcAudience { get; set; }
    public bool MagicLinkEnabled { get; set; }
}

public sealed class TenantAuthConfigEndpoint(ISender sender)
    : Endpoint<UpsertTenantAuthConfigRequest, TenantAuthConfigDto>
{
    public override void Configure()
    {
        Put("/tenant/auth-config");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Tenant"));
    }

    public override async Task HandleAsync(UpsertTenantAuthConfigRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpsertTenantAuthConfigCommand
        {
            AllowBuiltInAuth = req.AllowBuiltInAuth,
            OidcIssuer = req.OidcIssuer,
            OidcClientId = req.OidcClientId,
            OidcAudience = req.OidcAudience,
            MagicLinkEnabled = req.MagicLinkEnabled
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
