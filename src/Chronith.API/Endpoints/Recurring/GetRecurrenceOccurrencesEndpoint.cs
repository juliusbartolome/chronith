using Chronith.Application.Queries.Recurring;
using Chronith.Domain.Models;
using FastEndpoints;
using FluentValidation;
using MediatR;

namespace Chronith.API.Endpoints.Recurring;

public sealed class GetRecurrenceOccurrencesRequest
{
    public Guid Id { get; set; }

    [QueryParam]
    public DateOnly From { get; set; }

    [QueryParam]
    public DateOnly To { get; set; }
}

public sealed class GetRecurrenceOccurrencesRequestValidator : Validator<GetRecurrenceOccurrencesRequest>
{
    public GetRecurrenceOccurrencesRequestValidator()
    {
        RuleFor(x => x.From).Must(f => f != default).WithMessage("'From' query parameter is required.");
        RuleFor(x => x.To).Must(t => t != default).WithMessage("'To' query parameter is required.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From != default && x.To != default)
            .WithMessage("'To' must be on or after 'From'.");
    }
}

public sealed class GetRecurrenceOccurrencesEndpoint(ISender sender)
    : Endpoint<GetRecurrenceOccurrencesRequest, IReadOnlyList<DateOnly>>
{
    public override void Configure()
    {
        Get("/recurring/{id}/occurrences");
        Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.BookingsRead}");
        Options(x => x.WithTags("Recurring").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(GetRecurrenceOccurrencesRequest req, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetRecurrenceOccurrencesQuery(req.Id, req.From, req.To), ct);
        await Send.OkAsync(result, ct);
    }
}
