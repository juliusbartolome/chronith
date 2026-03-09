using Chronith.Application.Commands.CustomerAuth.Refresh;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerRefreshRequest
{
    public string Slug { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class CustomerRefreshEndpoint(ISender sender)
    : Endpoint<CustomerRefreshRequest, CustomerAuthTokenDto>
{
    public override void Configure()
    {
        Post("/public/{slug}/auth/refresh");
        AllowAnonymous();
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CustomerRefreshRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CustomerRefreshCommand(req.Slug, req.RefreshToken), ct);
        await Send.OkAsync(result, ct);
    }
}
