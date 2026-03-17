using Chronith.Application.Commands.CustomerAuth.MagicLink;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerMagicLinkVerifyRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public sealed class CustomerMagicLinkVerifyEndpoint(ISender sender)
    : Endpoint<CustomerMagicLinkVerifyRequest, CustomerAuthTokenDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/magic-link/verify");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(CustomerMagicLinkVerifyRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = req.Slug,
            Token = req.Token
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
