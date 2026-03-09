using Chronith.Application.Commands.Recurring.UpdateRecurrenceRule;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class UpdateRecurrenceRuleRequest
{
    // Route param
    public Guid Id { get; set; }

    // Body
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; set; }
    public DateOnly SeriesStart { get; set; }
    public DateOnly? SeriesEnd { get; set; }
    public int? MaxOccurrences { get; set; }
}

public sealed class UpdateRecurrenceRuleEndpoint(ISender sender)
    : Endpoint<UpdateRecurrenceRuleRequest, RecurrenceRuleDto>
{
    public override void Configure()
    {
        Put("/recurring/{id}");
        Roles("TenantAdmin", "TenantStaff");
        Options(x => x.WithTags("Recurring"));
    }

    public override async Task HandleAsync(UpdateRecurrenceRuleRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRecurrenceRuleCommand
        {
            Id = req.Id,
            Frequency = req.Frequency,
            Interval = req.Interval,
            DaysOfWeek = req.DaysOfWeek,
            SeriesStart = req.SeriesStart,
            SeriesEnd = req.SeriesEnd,
            MaxOccurrences = req.MaxOccurrences
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
