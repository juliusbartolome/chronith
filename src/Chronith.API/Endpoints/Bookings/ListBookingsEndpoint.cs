using Chronith.Application.DTOs;
using Chronith.Application.Queries.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Bookings;

public sealed class ListBookingsRequest
{
    // Route param
    public string Slug { get; set; } = string.Empty;

    [QueryParam]
    public int Page { get; set; } = 1;

    [QueryParam]
    public int PageSize { get; set; } = 20;
}

public sealed class ListBookingsEndpoint(ISender sender)
    : Endpoint<ListBookingsRequest, PagedResultDto<BookingDto>>
{
    public override void Configure()
    {
        Get("/booking-types/{slug}/bookings");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Bookings"));
    }

    public override async Task HandleAsync(ListBookingsRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new ListBookingsQuery
        {
            BookingTypeSlug = req.Slug,
            BookingTypeId = Guid.Empty,
            Page = req.Page,
            PageSize = req.PageSize
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
