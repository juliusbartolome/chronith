using Chronith.Application.Interfaces;

namespace Chronith.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ChronithDbContext _db;

    public UnitOfWork(ChronithDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
