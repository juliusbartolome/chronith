using Chronith.Application.Commands.CustomerAuth.Register;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerRegisterRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class CustomerRegisterEndpoint(ISender sender)
    : Endpoint<CustomerRegisterRequest, CustomerAuthTokenDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/register");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Auth"));
    }

    public override async Task HandleAsync(CustomerRegisterRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CustomerRegisterCommand
        {
            TenantSlug = req.Slug,
            Email = req.Email,
            Password = req.Password,
            Name = req.Name,
            Phone = req.Phone
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
