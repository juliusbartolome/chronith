using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.BookingTypes;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateBookingTypeCommand : IRequest<BookingTypeDto>
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required bool IsTimeSlot { get; init; }
    public int Capacity { get; init; } = 1;
    public required Domain.Enums.PaymentMode PaymentMode { get; init; }
    public string? PaymentProvider { get; init; }
    public long PriceInCentavos { get; init; }
    public string Currency { get; init; } = "PHP";
    public bool RequiresStaffAssignment { get; init; }

    // TimeSlot fields
    public int DurationMinutes { get; init; }
    public int BufferBeforeMinutes { get; init; }
    public int BufferAfterMinutes { get; init; }
    public IReadOnlyList<TimeSlotWindowDto>? AvailabilityWindows { get; init; }

    // Calendar fields
    public IReadOnlyList<DayOfWeek>? AvailableDays { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateBookingTypeValidator : AbstractValidator<CreateBookingTypeCommand>
{
    public CreateBookingTypeValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must be lowercase alphanumeric with hyphens.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThan(0);
        RuleFor(x => x.PriceInCentavos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3);

        When(x => x.IsTimeSlot, () =>
        {
            RuleFor(x => x.DurationMinutes).GreaterThan(0);
            RuleFor(x => x.AvailabilityWindows).NotEmpty();
        });

        When(x => !x.IsTimeSlot, () =>
        {
            RuleFor(x => x.AvailableDays).NotEmpty();
        });
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateBookingTypeHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBookingTypeCommand, BookingTypeDto>
{
    public async Task<BookingTypeDto> Handle(
        CreateBookingTypeCommand cmd, CancellationToken ct)
    {
        if (await repository.SlugExistsAsync(tenantContext.TenantId, cmd.Slug, ct))
            throw new SlugAlreadyExistsException(cmd.Slug);

        BookingType bookingType;
        if (cmd.IsTimeSlot)
        {
            var windows = (cmd.AvailabilityWindows ?? [])
                .Select(w => new TimeSlotWindow(w.DayOfWeek, w.StartTime, w.EndTime))
                .ToList();

            bookingType = TimeSlotBookingType.Create(
                tenantContext.TenantId,
                cmd.Slug,
                cmd.Name,
                cmd.Capacity,
                cmd.PaymentMode,
                cmd.PaymentProvider,
                cmd.DurationMinutes,
                cmd.BufferBeforeMinutes,
                cmd.BufferAfterMinutes,
                windows,
                cmd.PriceInCentavos,
                cmd.Currency,
                cmd.RequiresStaffAssignment);
        }
        else
        {
            bookingType = CalendarBookingType.Create(
                tenantContext.TenantId,
                cmd.Slug,
                cmd.Name,
                cmd.Capacity,
                cmd.PaymentMode,
                cmd.PaymentProvider,
                cmd.AvailableDays ?? [],
                cmd.PriceInCentavos,
                cmd.Currency,
                cmd.RequiresStaffAssignment);
        }

        await repository.AddAsync(bookingType, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return bookingType.ToDto();
    }
}
