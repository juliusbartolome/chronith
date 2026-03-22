using Chronith.Application.DTOs;
using Chronith.Application.Queries.ApiKeys;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class ListApiKeysEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<ApiKeyDto>>
{
    public override void Configure()
    {
        Get("/tenant/api-keys");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("ApiKeys").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListApiKeysQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
