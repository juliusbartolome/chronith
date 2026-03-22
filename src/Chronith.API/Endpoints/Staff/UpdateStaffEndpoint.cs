using Chronith.Application.Commands.Staff;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class UpdateStaffRequest
{
    // Route param
    public Guid Id { get; set; }

    // Body
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<StaffAvailabilityWindowInput> AvailabilityWindows { get; set; } = [];
}

public sealed class UpdateStaffEndpoint(ISender sender)
    : Endpoint<UpdateStaffRequest, StaffMemberDto>
{
    public override void Configure()
    {
        Put("/staff/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.StaffWrite}");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateStaffRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateStaffCommand
        {
            StaffId = req.Id,
            Name = req.Name,
            Email = req.Email,
            AvailabilityWindows = req.AvailabilityWindows
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
