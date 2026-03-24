using Chronith.Application.Commands.CustomerAuth.MagicLink;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerMagicLinkRegisterRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Mobile { get; set; }
}

public sealed class CustomerMagicLinkRegisterEndpoint(ISender sender)
    : Endpoint<CustomerMagicLinkRegisterRequest, MagicLinkInitiatedDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/magic-link/register");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(CustomerMagicLinkRegisterRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = req.Slug,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Mobile = req.Mobile
        }, ct);

        await Send.ResponseAsync(result, 202, ct);
    }
}
