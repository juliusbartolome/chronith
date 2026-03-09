using System.Security.Claims;
using Chronith.Application.DTOs;
using Chronith.Application.Queries.CustomerAuth.GetCustomerBookings;
using Chronith.Domain.Exceptions;
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
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerId = User.FindFirstValue("customer_id")
            ?? throw new UnauthorizedException("Missing customer_id claim");
        var customerGuid = Guid.Parse(customerId);

        var result = await sender.Send(new GetCustomerBookingsQuery(customerGuid), ct);
        await Send.OkAsync(result, ct);
    }
}
