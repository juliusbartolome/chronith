using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.BookingTypes;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record DeleteBookingTypeCommand(string Slug) : IRequest, IAuditable
{
    // Slug-based delete — no Guid available pre-execution
    public Guid EntityId => Guid.Empty;
    public string EntityType => "BookingType";
    public string Action => "Delete";
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class DeleteBookingTypeHandler(
    ITenantContext tenantContext,
    IBookingTypeRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteBookingTypeCommand>
{
    public async Task Handle(DeleteBookingTypeCommand cmd, CancellationToken ct)
    {
        var bt = await repository.GetBySlugAsync(tenantContext.TenantId, cmd.Slug, ct)
            ?? throw new NotFoundException("BookingType", cmd.Slug);

        bt.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
