namespace Chronith.Application.Interfaces;

/// <summary>Wraps a unit-of-work commit and transaction management.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins an explicit database transaction and returns a handle.
    /// The caller must Commit or Dispose/Rollback the returned handle.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>Handle for an explicit database transaction.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>Acquires a PostgreSQL transaction-scoped advisory lock for <paramref name="key"/>.</summary>
    Task AcquireAdvisoryLockAsync(long key, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
