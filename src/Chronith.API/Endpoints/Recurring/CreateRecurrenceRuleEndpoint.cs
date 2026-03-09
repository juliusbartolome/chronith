using Chronith.Application.Commands.Recurring.CreateRecurrenceRule;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class CreateRecurrenceRuleRequest
{
    // Route param
    public string Slug { get; set; } = string.Empty;

    // Body
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; set; }
    public DateOnly SeriesStart { get; set; }
    public DateOnly? SeriesEnd { get; set; }
    public int? MaxOccurrences { get; set; }
}

public sealed class CreateRecurrenceRuleEndpoint(ISender sender)
    : Endpoint<CreateRecurrenceRuleRequest, RecurrenceRuleDto>
{
    public override void Configure()
    {
        Post("/booking-types/{slug}/recurring");
        Roles("TenantAdmin", "TenantStaff", "Customer");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(CreateRecurrenceRuleRequest req, CancellationToken ct)
    {
        var command = new CreateRecurrenceRuleCommand
        {
            BookingTypeSlug = req.Slug,
            Frequency = req.Frequency,
            Interval = req.Interval,
            DaysOfWeek = req.DaysOfWeek,
            SeriesStart = req.SeriesStart,
            SeriesEnd = req.SeriesEnd,
            MaxOccurrences = req.MaxOccurrences
        };

        var result = await sender.Send(command, ct);
        await Send.CreatedAtAsync<GetRecurrenceRuleEndpoint>(
            new { id = result.Id }, result, cancellation: ct);
    }
}
