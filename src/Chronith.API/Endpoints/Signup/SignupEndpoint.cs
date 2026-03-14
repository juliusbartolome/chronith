using Chronith.Application.Commands.Signup;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Signup;

public sealed class SignupRequest
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SignupEndpoint(ISender sender)
    : Endpoint<SignupRequest, SignupResultDto>
{
    public override void Configure()
    {
        Post("/signup");
        AllowAnonymous();
        Options(x => x.WithTags("Signup").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(SignupRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new SignupCommand
        {
            TenantName = req.TenantName,
            TenantSlug = req.TenantSlug,
            TimeZoneId = req.TimeZoneId,
            Email = req.Email,
            Password = req.Password,
        }, ct);
        await Send.ResponseAsync(result, 201, ct);
    }
}
