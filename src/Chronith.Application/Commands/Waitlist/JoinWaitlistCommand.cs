using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Waitlist;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record JoinWaitlistCommand : IRequest<WaitlistEntryDto>, IAuditable
{
    public required string BookingTypeSlug { get; init; }
    public required DateTimeOffset DesiredStart { get; init; }
    public required DateTimeOffset DesiredEnd { get; init; }

    // IAuditable — EntityId is Guid.Empty pre-creation
    public Guid EntityId => Guid.Empty;
    public string EntityType => "WaitlistEntry";
    public string Action => "Create";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class JoinWaitlistValidator : AbstractValidator<JoinWaitlistCommand>
{
    public JoinWaitlistValidator()
    {
        RuleFor(x => x.BookingTypeSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DesiredEnd).GreaterThan(x => x.DesiredStart);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class JoinWaitlistHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository bookingTypeRepo,
    IWaitlistRepository waitlistRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<JoinWaitlistCommand, WaitlistEntryDto>
{
    public async Task<WaitlistEntryDto> Handle(JoinWaitlistCommand cmd, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(tenantContext.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var entry = WaitlistEntry.Create(
            tenantId: tenantContext.TenantId,
            bookingTypeId: bookingType.Id,
            staffMemberId: null,
            customerId: tenantContext.UserId,
            customerEmail: tenantContext.UserId, // Will be enriched later
            desiredStart: cmd.DesiredStart,
            desiredEnd: cmd.DesiredEnd);

        await waitlistRepo.AddAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return entry.ToDto();
    }
}
