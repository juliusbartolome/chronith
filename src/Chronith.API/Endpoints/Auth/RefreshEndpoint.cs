using Chronith.Application.Commands.Auth.Refresh;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RefreshEndpoint(ISender sender) : Endpoint<RefreshRequest, AuthTokenDto>
{
    public override void Configure()
    {
        Post("/auth/refresh");
        AllowAnonymous();
        Options(x => x.WithTags("Auth").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(RefreshRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new RefreshTokenCommand(req.RefreshToken), ct);
        await Send.OkAsync(result, ct);
    }
}
