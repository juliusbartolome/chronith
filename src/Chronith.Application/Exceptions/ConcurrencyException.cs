namespace Chronith.Application.Exceptions;

/// <summary>
/// Thrown when a concurrent modification conflict is detected during a save.
/// Infrastructure catches DbUpdateConcurrencyException and rethrows this.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException() : base("A concurrency conflict occurred.") { }
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
}
