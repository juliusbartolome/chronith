using Chronith.Application.Commands.TimeBlocks;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.TimeBlocks;

public sealed class CreateTimeBlockRequest
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public Guid? BookingTypeId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public string? Reason { get; set; }
}

public sealed class CreateTimeBlockEndpoint(ISender sender)
    : Endpoint<CreateTimeBlockRequest, TimeBlockDto>
{
    public override void Configure()
    {
        Post("/time-blocks");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TimeBlocksWrite}");
        Options(x => x.WithTags("TimeBlocks").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateTimeBlockRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTimeBlockCommand
        {
            Start = req.Start,
            End = req.End,
            BookingTypeId = req.BookingTypeId,
            StaffMemberId = req.StaffMemberId,
            Reason = req.Reason
        }, ct);

        await Send.CreatedAtAsync<CreateTimeBlockEndpoint>(
            null, result, cancellation: ct);
    }
}
