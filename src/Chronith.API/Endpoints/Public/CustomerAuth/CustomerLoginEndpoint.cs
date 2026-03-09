using Chronith.Application.Commands.CustomerAuth.Login;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerLoginRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CustomerLoginEndpoint(ISender sender)
    : Endpoint<CustomerLoginRequest, CustomerAuthTokenDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/login");
        AllowAnonymous();
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CustomerLoginRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CustomerLoginCommand(req.Slug, req.Email, req.Password), ct);
        await Send.OkAsync(result, ct);
    }
}
