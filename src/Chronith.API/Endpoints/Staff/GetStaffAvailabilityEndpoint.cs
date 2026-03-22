using Chronith.Application.DTOs;
using Chronith.Application.Queries.Staff;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class GetStaffAvailabilityRequest
{
    // Route param
    public Guid Id { get; set; }

    // Query params
    [QueryParam]
    public DateTimeOffset From { get; set; }

    [QueryParam]
    public DateTimeOffset To { get; set; }
}

public sealed class GetStaffAvailabilityEndpoint(ISender sender)
    : Endpoint<GetStaffAvailabilityRequest, AvailabilityDto>
{
    public override void Configure()
    {
        Get("/staff/{id}/availability");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.StaffRead}");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetStaffAvailabilityRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetStaffAvailabilityQuery
        {
            StaffId = req.Id,
            From = req.From,
            To = req.To
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
