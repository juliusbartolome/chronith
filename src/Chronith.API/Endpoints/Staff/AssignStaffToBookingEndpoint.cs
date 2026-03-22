using Chronith.Application.Commands.Staff;
using Chronith.Application.DTOs;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Staff;

public sealed class AssignStaffToBookingRequest
{
    // Route param
    public Guid BookingId { get; set; }

    // Body
    public Guid StaffMemberId { get; set; }
}

public sealed class AssignStaffToBookingEndpoint(ISender sender)
    : Endpoint<AssignStaffToBookingRequest, BookingDto>
{
    public override void Configure()
    {
        Post("/bookings/{bookingId}/assign-staff");
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.StaffWrite}");
        Options(x => x.WithTags("Staff").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(AssignStaffToBookingRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new AssignStaffToBookingCommand
        {
            BookingId = req.BookingId,
            StaffMemberId = req.StaffMemberId
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
