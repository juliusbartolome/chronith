using Chronith.Application.DTOs;
using Chronith.Application.Queries.CustomerAuth.GetCustomerBookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerBookingsEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<BookingDto>>
{
    public override void Configure()
    {
        Get("/public/{slug}/my-bookings");
        Roles("Customer");
        Options(x => x.WithTags("Public").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerGuid = User.GetCustomerIdFromClaims();

        var result = await sender.Send(new GetCustomerBookingsQuery(customerGuid), ct);
        await Send.OkAsync(result, ct);
    }
}
