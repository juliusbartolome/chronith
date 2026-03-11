using Chronith.Application.DTOs;
using Chronith.Application.Queries.Audit;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Audit;

public sealed class GetAuditEntriesRequest
{
    [QueryParam]
    public string? EntityType { get; set; }

    [QueryParam]
    public Guid? EntityId { get; set; }

    [QueryParam]
    public string? UserId { get; set; }

    [QueryParam]
    public string? Action { get; set; }

    [QueryParam]
    public DateTimeOffset? From { get; set; }

    [QueryParam]
    public DateTimeOffset? To { get; set; }

    [QueryParam]
    public int Page { get; set; } = 1;

    [QueryParam]
    public int PageSize { get; set; } = 20;
}

public sealed class GetAuditEntriesEndpoint(ISender sender)
    : Endpoint<GetAuditEntriesRequest, PagedResultDto<AuditEntryDto>>
{
    public override void Configure()
    {
        Get("/audit");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Audit").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetAuditEntriesRequest req, CancellationToken ct)
    {
        var page = req.Page < 1 ? 1 : req.Page;
        var pageSize = req.PageSize < 1 ? 1 : req.PageSize > 100 ? 100 : req.PageSize;

        var result = await sender.Send(new GetAuditEntriesQuery(
            EntityType: req.EntityType,
            EntityId: req.EntityId,
            UserId: req.UserId,
            Action: req.Action,
            From: req.From,
            To: req.To,
            Page: page,
            PageSize: pageSize), ct);

        await Send.OkAsync(result, ct);
    }
}
