using Chronith.Application.Commands.ApiKeys;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class RevokeApiKeyRequest
{
    public Guid Id { get; set; }
}

public sealed class RevokeApiKeyEndpoint(ISender sender)
    : Endpoint<RevokeApiKeyRequest>
{
    public override void Configure()
    {
        Delete("/tenant/api-keys/{id}");
        Roles("TenantAdmin");
        // Intentionally Bearer-only: API keys cannot revoke other API keys.
    }

    public override async Task HandleAsync(RevokeApiKeyRequest req, CancellationToken ct)
    {
        await sender.Send(new RevokeApiKeyCommand(req.Id), ct);
        await Send.NoContentAsync(ct);
    }
}
