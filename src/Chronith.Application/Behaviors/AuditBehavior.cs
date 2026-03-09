using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse>(
    IEnumerable<IAuditSnapshotResolver> resolvers,
    IAuditEntryRepository auditRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IAuditable auditable)
            return await next(ct);

        var resolver = resolvers.FirstOrDefault(r => r.EntityType == auditable.EntityType);
        var oldValues = resolver is not null
            ? await resolver.ResolveSnapshotAsync(auditable.EntityId, ct)
            : null;

        var response = await next(ct);

        var newValues = resolver is not null
            ? await resolver.ResolveSnapshotAsync(auditable.EntityId, ct)
            : null;

        var entry = AuditEntry.Create(
            tenantContext.TenantId,
            tenantContext.UserId,
            tenantContext.Role,
            auditable.EntityType,
            auditable.EntityId,
            auditable.Action,
            oldValues,
            newValues,
            null);

        await auditRepository.AddAsync(entry, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return response;
    }
}
