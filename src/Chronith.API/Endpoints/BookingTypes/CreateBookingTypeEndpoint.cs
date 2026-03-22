using Chronith.Application.Commands.BookingTypes;
using Chronith.Application.DTOs;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.BookingTypes;

public sealed class CreateBookingTypeRequest
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsTimeSlot { get; set; }
    public int Capacity { get; set; } = 1;
    public Chronith.Domain.Enums.PaymentMode PaymentMode { get; set; }
    public string? PaymentProvider { get; set; }
    public long PriceInCentavos { get; set; }
    public string Currency { get; set; } = "PHP";
    public bool RequiresStaffAssignment { get; set; }
    public int DurationMinutes { get; set; }
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }
    public IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows { get; set; }
    public IReadOnlyList<DayOfWeek>? AvailableDays { get; set; }
}

public sealed class CreateBookingTypeEndpoint(ISender sender)
    : Endpoint<CreateBookingTypeRequest, BookingTypeDto>
{
    public override void Configure()
    {
        Post("/booking-types");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingTypesWrite}");
        Options(x => x.WithTags("BookingTypes").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateBookingTypeRequest req, CancellationToken ct)
    {
        var command = new CreateBookingTypeCommand
        {
            Slug = req.Slug,
            Name = req.Name,
            IsTimeSlot = req.IsTimeSlot,
            Capacity = req.Capacity,
            PaymentMode = req.PaymentMode,
            PaymentProvider = req.PaymentProvider,
            PriceInCentavos = req.PriceInCentavos,
            Currency = req.Currency,
            RequiresStaffAssignment = req.RequiresStaffAssignment,
            DurationMinutes = req.DurationMinutes,
            BufferBeforeMinutes = req.BufferBeforeMinutes,
            BufferAfterMinutes = req.BufferAfterMinutes,
            AvailabilityWindows = req.AvailabilityWindows,
            AvailableDays = req.AvailableDays
        };

        var result = await sender.Send(command, ct);
        await Send.CreatedAtAsync<GetBookingTypeEndpoint>(
            new { slug = result.Slug }, result, cancellation: ct);
    }
}
