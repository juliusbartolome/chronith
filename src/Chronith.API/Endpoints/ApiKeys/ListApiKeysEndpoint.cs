using Chronith.Application.DTOs;
using Chronith.Application.Queries.ApiKeys;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class ListApiKeysEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<ApiKeyDto>>
{
    public override void Configure()
    {
        Get("/tenant/api-keys");
        Roles("TenantAdmin");
        AuthSchemes("Bearer", "ApiKey");
        Options(x => x.WithTags("ApiKeys").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListApiKeysQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
