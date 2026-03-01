using Chronith.Domain.Enums;

namespace Chronith.Domain.Exceptions;
public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(BookingStatus from, string action)
        : base($"Cannot perform '{action}' on a booking in '{from}' status.") { }
}
