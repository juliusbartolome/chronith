using Chronith.Application.Commands.Auth.Login;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Auth;

public sealed class LoginRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginEndpoint(ISender sender) : Endpoint<LoginRequest, AuthTokenDto>
{
    public override void Configure()
    {
        Post("/auth/login");
        AllowAnonymous();
        Options(x => x.WithTags("Auth").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new LoginCommand(req.TenantSlug, req.Email, req.Password), ct);
        await Send.OkAsync(result, ct);
    }
}
