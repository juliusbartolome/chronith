using Chronith.Application.Commands.Staff;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class DeleteStaffRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteStaffEndpoint(ISender sender)
    : Endpoint<DeleteStaffRequest>
{
    public override void Configure()
    {
        Delete("/staff/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.StaffWrite}");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(DeleteStaffRequest req, CancellationToken ct)
    {
        await sender.Send(new DeleteStaffCommand
        {
            StaffId = req.Id
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
