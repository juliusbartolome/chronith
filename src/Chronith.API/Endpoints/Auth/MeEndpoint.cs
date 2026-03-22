using Chronith.Application.Commands.Auth.UpdateMe;
using Chronith.Application.DTOs;
using Chronith.Application.Queries.Auth.GetMe;
using FastEndpoints;
using MediatR;
using System.Security.Claims;

namespace Chronith.API.Endpoints.Auth;

public sealed class GetMeEndpoint(ISender sender) : EndpointWithoutRequest<UserProfileDto>
{
    public override void Configure()
    {
        Get("/auth/me");
        Roles("TenantAdmin", "TenantStaff");
        AuthSchemes("Bearer");
        Options(x => x.WithTags("Auth").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        var userId = Guid.Parse(rawId!);
        var result = await sender.Send(new GetMeQuery(userId), ct);
        await Send.OkAsync(result, ct);
    }
}

public sealed class PatchMeRequest
{
    public string? Email { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class PatchMeEndpoint(ISender sender) : Endpoint<PatchMeRequest, UserProfileDto>
{
    public override void Configure()
    {
        Patch("/auth/me");
        Roles("TenantAdmin", "TenantStaff");
        AuthSchemes("Bearer");
        Options(x => x.WithTags("Auth").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(PatchMeRequest req, CancellationToken ct)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub");
        var userId = Guid.Parse(rawId!);
        var result = await sender.Send(new UpdateMeCommand(userId, req.Email, req.NewPassword), ct);
        await Send.OkAsync(result, ct);
    }
}
