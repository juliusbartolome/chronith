using Chronith.Application.DTOs;
using Chronith.Application.Queries.BookingTypes;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class ListBookingTypesEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<BookingTypeDto>>
{
    public override void Configure()
    {
        Get("/booking-types");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingTypesRead}");
        Options(x => x.WithTags("BookingTypes").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListBookingTypesQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
