using Chronith.Application.Commands.BookingTypes;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class UpdateBookingTypeRequest
{
    // Route param
    public string Slug { get; set; } = string.Empty;

    // Body
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1;
    public Chronith.Domain.Enums.PaymentMode PaymentMode { get; set; }
    public string? PaymentProvider { get; set; }
    public long PriceInCentavos { get; set; }
    public string Currency { get; set; } = "PHP";
    public int DurationMinutes { get; set; }
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }
    public IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows { get; set; }
    public IReadOnlyList<DayOfWeek>? AvailableDays { get; set; }
}

public sealed class UpdateBookingTypeEndpoint(ISender sender)
    : Endpoint<UpdateBookingTypeRequest, BookingTypeDto>
{
    public override void Configure()
    {
        Put("/booking-types/{slug}");
        Roles("TenantAdmin");
    }

    public override async Task HandleAsync(UpdateBookingTypeRequest req, CancellationToken ct)
    {
        var command = new UpdateBookingTypeCommand
        {
            Slug = req.Slug,
            Name = req.Name,
            Capacity = req.Capacity,
            PaymentMode = req.PaymentMode,
            PaymentProvider = req.PaymentProvider,
            PriceInCentavos = req.PriceInCentavos,
            Currency = req.Currency,
            DurationMinutes = req.DurationMinutes,
            BufferBeforeMinutes = req.BufferBeforeMinutes,
            BufferAfterMinutes = req.BufferAfterMinutes,
            AvailabilityWindows = req.AvailabilityWindows,
            AvailableDays = req.AvailableDays
        };

        var result = await sender.Send(command, ct);
        await Send.OkAsync(result, ct);
    }
}
