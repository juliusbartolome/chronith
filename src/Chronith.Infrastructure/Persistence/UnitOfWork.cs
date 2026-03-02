using Chronith.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Chronith.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ChronithDbContext _db;

    public UnitOfWork(ChronithDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new UnitOfWorkTransaction(_db, tx);
    }
}

internal sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly ChronithDbContext _db;
    private readonly IDbContextTransaction _tx;

    public UnitOfWorkTransaction(ChronithDbContext db, IDbContextTransaction tx)
    {
        _db = db;
        _tx = tx;
    }

    /// <inheritdoc/>
    public async Task AcquireAdvisoryLockAsync(long key, CancellationToken ct = default)
    {
        // pg_advisory_xact_lock acquires a transaction-scoped exclusive advisory lock.
        // It blocks until the lock is obtained and is released automatically on commit/rollback.
        await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
        await _tx.CommitAsync(ct);
    }

    public ValueTask DisposeAsync() => _tx.DisposeAsync();
}
