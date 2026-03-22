using Chronith.Application.Commands.TenantSettings;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

public sealed class UpdateTenantSettingsEndpoint(ISender sender)
    : Endpoint<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    public override void Configure()
    {
        Put("/tenant/settings");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateTenantSettingsCommand req, CancellationToken ct)
    {
        var result = await sender.Send(req, ct);
        await Send.OkAsync(result, ct);
    }
}
