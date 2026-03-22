using Chronith.Application.Commands.Recurring.UpdateRecurrenceRule;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class UpdateRecurrenceRuleRequest
{
    // Route param
    public Guid Id { get; set; }

    // Body
    public Guid? StaffMemberId { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeSpan Duration { get; set; }
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
        Roles("TenantAdmin", "TenantStaff", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsWrite}");
        Options(x => x.WithTags("Recurring").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateRecurrenceRuleRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new UpdateRecurrenceRuleCommand
        {
            Id = req.Id,
            StaffMemberId = req.StaffMemberId,
            Frequency = req.Frequency,
            Interval = req.Interval,
            DaysOfWeek = req.DaysOfWeek,
            StartTime = req.StartTime,
            Duration = req.Duration,
            SeriesStart = req.SeriesStart,
            SeriesEnd = req.SeriesEnd,
            MaxOccurrences = req.MaxOccurrences
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
