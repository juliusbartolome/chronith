namespace Chronith.Domain.Exceptions;
public sealed class SlotConflictException : DomainException
{
    public SlotConflictException() : base("The requested slot is not available.") { }
}
