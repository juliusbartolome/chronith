using Chronith.Application.Commands.Waitlist;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Waitlist;

public sealed class AcceptWaitlistOfferRequest
{
    public Guid Id { get; set; }
}

public sealed class AcceptWaitlistOfferEndpoint(ISender sender)
    : Endpoint<AcceptWaitlistOfferRequest, WaitlistEntryDto>
{
    public override void Configure()
    {
        Post("/waitlist/{id}/accept");
        Roles("Customer");
        Options(x => x.WithTags("Waitlist").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(AcceptWaitlistOfferRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new AcceptWaitlistOfferCommand
        {
            WaitlistEntryId = req.Id
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
