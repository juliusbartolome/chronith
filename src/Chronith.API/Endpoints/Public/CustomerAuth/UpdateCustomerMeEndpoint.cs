using Chronith.Application.Commands.CustomerAuth.UpdateProfile;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class UpdateCustomerMeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class UpdateCustomerMeEndpoint(ISender sender)
    : Endpoint<UpdateCustomerMeRequest, CustomerDto>
{
    public override void Configure()
    {
        Put("/public/{slug}/auth/me");
        Roles("Customer");
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(UpdateCustomerMeRequest req, CancellationToken ct)
    {
        var customerGuid = User.GetCustomerIdFromClaims();

        var result = await sender.Send(new UpdateCustomerProfileCommand
        {
            CustomerId = customerGuid,
            Name = req.Name,
            Phone = req.Phone
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
