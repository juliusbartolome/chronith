using Chronith.Application.DTOs;
using Chronith.Application.Queries.CustomerAuth.GetCustomerMe;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerMeEndpoint(ISender sender) : EndpointWithoutRequest<CustomerDto>
{
    public override void Configure()
    {
        Get("/public/{slug}/auth/me");
        Roles("Customer");
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerGuid = User.GetCustomerIdFromClaims();

        var result = await sender.Send(new GetCustomerMeQuery(customerGuid), ct);
        await Send.OkAsync(result, ct);
    }
}
