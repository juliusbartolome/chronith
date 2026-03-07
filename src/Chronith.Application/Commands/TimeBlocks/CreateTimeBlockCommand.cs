using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TimeBlocks;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateTimeBlockCommand : IRequest<TimeBlockDto>
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public Guid? BookingTypeId { get; init; }
    public Guid? StaffMemberId { get; init; }
    public string? Reason { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateTimeBlockValidator : AbstractValidator<CreateTimeBlockCommand>
{
    public CreateTimeBlockValidator()
    {
        RuleFor(x => x.End).GreaterThan(x => x.Start);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateTimeBlockHandler(
    ITenantContext tenantContext,
    ITimeBlockRepository timeBlockRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTimeBlockCommand, TimeBlockDto>
{
    public async Task<TimeBlockDto> Handle(CreateTimeBlockCommand cmd, CancellationToken ct)
    {
        var block = TimeBlock.Create(
            tenantId: tenantContext.TenantId,
            bookingTypeId: cmd.BookingTypeId,
            staffMemberId: cmd.StaffMemberId,
            start: cmd.Start,
            end: cmd.End,
            reason: cmd.Reason);

        await timeBlockRepo.AddAsync(block, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return block.ToDto();
    }
}
