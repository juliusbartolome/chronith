using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.BookingTypes;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateBookingTypeCommand : IRequest<BookingTypeDto>
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public int Capacity { get; init; } = 1;
    public required Domain.Enums.PaymentMode PaymentMode { get; init; }
    public string? PaymentProvider { get; init; }
    public long PriceInCentavos { get; init; }
    public string Currency { get; init; } = "PHP";
    public bool RequiresStaffAssignment { get; init; }

    // TimeSlot fields (ignored for Calendar)
    public int DurationMinutes { get; init; }
    public int BufferBeforeMinutes { get; init; }
    public int BufferAfterMinutes { get; init; }
    public IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows { get; init; }

    // Calendar fields (ignored for TimeSlot)
    public IReadOnlyList<DayOfWeek>? AvailableDays { get; init; }

    /// <summary>Nullable HTTPS URL. Null removes the existing callback.</summary>
    public string? CustomerCallbackUrl { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateBookingTypeValidator : AbstractValidator<UpdateBookingTypeCommand>
{
    public UpdateBookingTypeValidator()
    {
        RuleFor(x => x.Slug).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThan(0);
        RuleFor(x => x.PriceInCentavos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);
        When(x => x.CustomerCallbackUrl is not null, () =>
        {
            RuleFor(x => x.CustomerCallbackUrl!)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
                .WithMessage("CustomerCallbackUrl must be a valid HTTPS URL.");
        });
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateBookingTypeHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateBookingTypeCommand, BookingTypeDto>
{
    public async Task<BookingTypeDto> Handle(
        UpdateBookingTypeCommand cmd, CancellationToken ct)
    {
        var bt = await repository.GetBySlugAsync(tenantContext.TenantId, cmd.Slug, ct)
            ?? throw new NotFoundException("BookingType", cmd.Slug);

        bt.Update(
            cmd.Name,
            cmd.Capacity,
            cmd.PaymentMode,
            cmd.PaymentProvider,
            cmd.DurationMinutes,
            cmd.BufferBeforeMinutes,
            cmd.BufferAfterMinutes,
            (cmd.AvailabilityWindows ?? [])
                .Select(w => new TimeSlotWindow(w.DayOfWeek, w.StartTime, w.EndTime))
                .ToList(),
            cmd.AvailableDays,
            cmd.PriceInCentavos,
            cmd.Currency,
            cmd.RequiresStaffAssignment);

        // Update customer callback — always called (null clears the URL)
        bt.SetCustomerCallback(cmd.CustomerCallbackUrl);

        await repository.UpdateAsync(bt, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return bt.ToDto();
    }
}
