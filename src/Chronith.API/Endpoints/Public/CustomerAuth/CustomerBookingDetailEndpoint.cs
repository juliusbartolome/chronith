using System.Security.Claims;
using Chronith.Application.DTOs;
using Chronith.Application.Queries.CustomerAuth.GetCustomerBookingDetail;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public sealed class CustomerBookingDetailRequest
{
    public string Slug { get; set; } = string.Empty;
    public Guid Id { get; set; }
}

public sealed class CustomerBookingDetailEndpoint(ISender sender)
    : Endpoint<CustomerBookingDetailRequest, BookingDto>
{
    public override void Configure()
    {
        Get("/public/{slug}/my-bookings/{id}");
        Roles("Customer");
        Options(x => x.WithTags("Public"));
    }

    public override async Task HandleAsync(CustomerBookingDetailRequest req, CancellationToken ct)
    {
        var customerId = User.FindFirstValue("customer_id")
            ?? throw new UnauthorizedException("Missing customer_id claim");
        var customerGuid = Guid.Parse(customerId);

        var result = await sender.Send(new GetCustomerBookingDetailQuery(customerGuid, req.Id), ct);
        await Send.OkAsync(result, ct);
    }
}
