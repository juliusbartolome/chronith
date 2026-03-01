namespace Chronith.Application.Interfaces;

/// <summary>Wraps a unit-of-work commit.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
