using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class CreateApiKeyRequest
{
    public string Description { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
}

public sealed class CreateApiKeyEndpoint(ISender sender)
    : Endpoint<CreateApiKeyRequest, CreateApiKeyResult>
{
    public override void Configure()
    {
        Post("/tenant/api-keys");
        Roles("TenantAdmin");
        Options(x => x.WithTags("ApiKeys").RequireRateLimiting("Authenticated"));
        // Intentionally Bearer-only: API keys cannot create other API keys.
    }

    public override async Task HandleAsync(CreateApiKeyRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateApiKeyCommand
        {
            Description = req.Description,
            Scopes = req.Scopes
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
