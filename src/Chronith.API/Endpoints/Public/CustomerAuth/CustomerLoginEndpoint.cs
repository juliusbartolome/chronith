using Chronith.Application.Commands.CustomerAuth.Login;
using Chronith.Application.Commands.CustomerAuth.OidcLogin;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerLoginRequest
{
    public string Slug { get; set; } = string.Empty;

    // Built-in auth fields
    public string? Email { get; set; }
    public string? Password { get; set; }

    // OIDC auth fields
    /// <summary>Set to "oidc" to use external identity provider login. Omit or set to "builtin" for email+password.</summary>
    public string Provider { get; set; } = "builtin";
    public string? Token { get; set; }
}

public sealed class CustomerLoginEndpoint(ISender sender)
    : Endpoint<CustomerLoginRequest, CustomerAuthTokenDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/login");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(CustomerLoginRequest req, CancellationToken ct)
    {
        if (string.Equals(req.Provider, "oidc", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(req.Token))
            {
                AddError(r => r.Token!, "Token is required for OIDC login.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            var result = await sender.Send(new CustomerOidcLoginCommand(req.Slug, req.Token), ct);
            await Send.OkAsync(result, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                AddError(r => r.Email!, "Email and Password are required for built-in login.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            var result = await sender.Send(new CustomerLoginCommand(req.Slug, req.Email, req.Password), ct);
            await Send.OkAsync(result, ct);
        }
    }
}
