using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record PublicJoinWaitlistCommand : IRequest<WaitlistEntryDto>
{
    public required Guid TenantId { get; init; }
    public required string BookingTypeSlug { get; init; }
    public required string CustomerId { get; init; }
    public required string CustomerEmail { get; init; }
    public required DateTimeOffset DesiredStart { get; init; }
    public required DateTimeOffset DesiredEnd { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PublicJoinWaitlistValidator : AbstractValidator<PublicJoinWaitlistCommand>
{
    public PublicJoinWaitlistValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BookingTypeSlug).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.DesiredEnd).GreaterThan(x => x.DesiredStart);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PublicJoinWaitlistHandler(
    IBookingTypeRepository bookingTypeRepo,
    IWaitlistRepository waitlistRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PublicJoinWaitlistCommand, WaitlistEntryDto>
{
    public async Task<WaitlistEntryDto> Handle(PublicJoinWaitlistCommand cmd, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetBySlugAsync(cmd.TenantId, cmd.BookingTypeSlug, ct)
            ?? throw new NotFoundException("BookingType", cmd.BookingTypeSlug);

        var entry = WaitlistEntry.Create(
            tenantId: cmd.TenantId,
            bookingTypeId: bookingType.Id,
            staffMemberId: null,
            customerId: cmd.CustomerId,
            customerEmail: cmd.CustomerEmail,
            desiredStart: cmd.DesiredStart,
            desiredEnd: cmd.DesiredEnd);

        await waitlistRepo.AddAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return entry.ToDto();
    }
}
