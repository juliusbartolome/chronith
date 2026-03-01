namespace Chronith.Domain.Exceptions;
public sealed class SlotNotInWindowException : DomainException
{
    public SlotNotInWindowException(DateTimeOffset requested)
        : base($"Requested start '{requested:O}' does not fall within any availability window.") { }
}
