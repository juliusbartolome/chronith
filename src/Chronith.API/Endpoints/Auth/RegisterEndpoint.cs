using Chronith.Application.Commands.Auth.Register;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Auth;

public sealed class RegisterRequest
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterEndpoint(ISender sender) : Endpoint<RegisterRequest, AuthTokenDto>
{
    public override void Configure()
    {
        Post("/auth/register");
        AllowAnonymous();
        Options(x => x.WithTags("Auth").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new RegisterTenantCommand(
            req.TenantName, req.TenantSlug, req.TimeZoneId, req.Email, req.Password), ct);
        await Send.ResponseAsync(result, 201, ct);
    }
}
