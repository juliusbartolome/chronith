using Chronith.Application.Commands.Waitlist;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Waitlist;

public sealed class RemoveFromWaitlistRequest
{
    public Guid Id { get; set; }
}

public sealed class RemoveFromWaitlistEndpoint(ISender sender)
    : Endpoint<RemoveFromWaitlistRequest>
{
    public override void Configure()
    {
        Delete("/waitlist/{id}");
        Roles("Customer", "TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsCancel}");
        Options(x => x.WithTags("Waitlist").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(RemoveFromWaitlistRequest req, CancellationToken ct)
    {
        var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
        var customerId = role == "Customer" ? User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value : null;

        await sender.Send(new RemoveFromWaitlistCommand
        {
            WaitlistEntryId = req.Id,
            RequiredCustomerId = customerId
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
