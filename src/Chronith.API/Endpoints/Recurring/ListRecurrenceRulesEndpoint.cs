using Chronith.Application.DTOs;
using Chronith.Application.Queries.Recurring;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class ListRecurrenceRulesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<RecurrenceRuleDto>>
{
    public override void Configure()
    {
        Get("/recurring");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsRead}");
        Options(x => x.WithTags("Recurring").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListRecurrenceRulesQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
