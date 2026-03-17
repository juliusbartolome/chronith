using Chronith.Application.Commands.Staff;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class CreateStaffRequest
{
    // Body
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? TenantUserId { get; set; }
    public IReadOnlyList<StaffAvailabilityWindowInput> AvailabilityWindows { get; set; } = [];
}

public sealed class CreateStaffEndpoint(ISender sender)
    : Endpoint<CreateStaffRequest, StaffMemberDto>
{
    public override void Configure()
    {
        Post("/staff");
        Roles("TenantAdmin");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateStaffRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateStaffCommand
        {
            Name = req.Name,
            Email = req.Email,
            TenantUserId = req.TenantUserId,
            AvailabilityWindows = req.AvailabilityWindows
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
