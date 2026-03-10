using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Waitlist;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RemoveFromWaitlistCommand : IRequest, IAuditable
{
    public required Guid WaitlistEntryId { get; init; }
    /// <summary>
    /// When the caller is a Customer, enforce ownership.
    /// </summary>
    public string? RequiredCustomerId { get; init; }

    public Guid EntityId => WaitlistEntryId;
    public string EntityType => "WaitlistEntry";
    public string Action => "Delete";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class RemoveFromWaitlistValidator : AbstractValidator<RemoveFromWaitlistCommand>
{
    public RemoveFromWaitlistValidator()
    {
        RuleFor(x => x.WaitlistEntryId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RemoveFromWaitlistHandler(
    ITenantContext tenantContext,
    IWaitlistRepository waitlistRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveFromWaitlistCommand>
{
    public async Task Handle(RemoveFromWaitlistCommand cmd, CancellationToken ct)
    {
        var entry = await waitlistRepo.GetByIdAsync(tenantContext.TenantId, cmd.WaitlistEntryId, ct)
            ?? throw new NotFoundException("WaitlistEntry", cmd.WaitlistEntryId);

        if (cmd.RequiredCustomerId is not null && entry.CustomerId != cmd.RequiredCustomerId)
            throw new UnauthorizedAccessException("Customers may only remove their own waitlist entries.");

        entry.SoftDelete();
        await waitlistRepo.UpdateAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
