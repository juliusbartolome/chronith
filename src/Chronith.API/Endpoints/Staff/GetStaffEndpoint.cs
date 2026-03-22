using Chronith.Application.DTOs;
using Chronith.Application.Queries.Staff;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class GetStaffRequest
{
    public Guid Id { get; set; }
}

public sealed class GetStaffEndpoint(ISender sender)
    : Endpoint<GetStaffRequest, StaffMemberDto>
{
    public override void Configure()
    {
        Get("/staff/{id}");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.StaffRead}");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetStaffRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new GetStaffQuery(req.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
