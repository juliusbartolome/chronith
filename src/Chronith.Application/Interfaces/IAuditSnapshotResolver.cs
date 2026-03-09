namespace Chronith.Application.Interfaces;

public interface IAuditSnapshotResolver
{
    string EntityType { get; }
    Task<string?> ResolveSnapshotAsync(Guid entityId, CancellationToken ct);
}
