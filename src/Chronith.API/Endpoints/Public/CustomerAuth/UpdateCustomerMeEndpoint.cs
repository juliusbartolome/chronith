using Chronith.Application.Commands.CustomerAuth.UpdateProfile;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class UpdateCustomerMeRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Mobile { get; set; }
}

public sealed class UpdateCustomerMeEndpoint(ISender sender)
    : Endpoint<UpdateCustomerMeRequest, CustomerDto>
{
    public override void Configure()
    {
        Put("/public/{slug}/auth/me");
        Roles("Customer");
        Options(x => x.WithTags("Public").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateCustomerMeRequest req, CancellationToken ct)
    {
        var customerGuid = User.GetCustomerIdFromClaims();

        var result = await sender.Send(new UpdateCustomerProfileCommand
        {
            CustomerId = customerGuid,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Mobile = req.Mobile
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
