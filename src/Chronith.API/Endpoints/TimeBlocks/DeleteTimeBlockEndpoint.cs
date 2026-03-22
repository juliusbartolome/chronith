using Chronith.Application.Commands.TimeBlocks;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.TimeBlocks;

public sealed class DeleteTimeBlockRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteTimeBlockEndpoint(ISender sender)
    : Endpoint<DeleteTimeBlockRequest>
{
    public override void Configure()
    {
        Delete("/time-blocks/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TimeBlocksWrite}");
        Options(x => x.WithTags("TimeBlocks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(DeleteTimeBlockRequest req, CancellationToken ct)
    {
        await sender.Send(new DeleteTimeBlockCommand
        {
            TimeBlockId = req.Id
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
