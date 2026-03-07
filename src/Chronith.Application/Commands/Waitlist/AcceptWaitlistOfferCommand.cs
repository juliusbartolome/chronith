using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Waitlist;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record AcceptWaitlistOfferCommand : IRequest<WaitlistEntryDto>
{
    public required Guid WaitlistEntryId { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class AcceptWaitlistOfferValidator : AbstractValidator<AcceptWaitlistOfferCommand>
{
    public AcceptWaitlistOfferValidator()
    {
        RuleFor(x => x.WaitlistEntryId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class AcceptWaitlistOfferHandler(
    ITenantContext tenantContext,
    IWaitlistRepository waitlistRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AcceptWaitlistOfferCommand, WaitlistEntryDto>
{
    public async Task<WaitlistEntryDto> Handle(AcceptWaitlistOfferCommand cmd, CancellationToken ct)
    {
        var entry = await waitlistRepo.GetByIdAsync(tenantContext.TenantId, cmd.WaitlistEntryId, ct)
            ?? throw new NotFoundException("WaitlistEntry", cmd.WaitlistEntryId);

        if (entry.CustomerId != tenantContext.UserId)
            throw new UnauthorizedAccessException("Customers may only accept their own waitlist offers.");

        entry.Accept();
        await waitlistRepo.UpdateAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return entry.ToDto();
    }
}
