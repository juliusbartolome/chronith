using Chronith.Application.DTOs;
using Chronith.Application.Queries.Audit;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Audit;

public sealed class GetAuditEntryByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetAuditEntryByIdEndpoint(ISender sender)
    : Endpoint<GetAuditEntryByIdRequest, AuditEntryDto>
{
    public override void Configure()
    {
        Get("/audit/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.AuditRead}");
        Options(x => x.WithTags("Audit").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetAuditEntryByIdRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetAuditEntryByIdQuery(req.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
