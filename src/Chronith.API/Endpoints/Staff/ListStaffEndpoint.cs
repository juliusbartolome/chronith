using Chronith.Application.DTOs;
using Chronith.Application.Queries.Staff;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class ListStaffEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<StaffMemberDto>>
{
    public override void Configure()
    {
        Get("/staff");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListStaffQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
