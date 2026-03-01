namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class Booking
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BookingTypeId { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public BookingStatus Status { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public string? PaymentReference { get; private set; }
    public bool IsDeleted { get; private set; }
    public uint RowVersion { get; private set; }

    private readonly List<BookingStatusChange> _statusChanges = new();
    public IReadOnlyList<BookingStatusChange> StatusChanges => _statusChanges.AsReadOnly();

    // For Infrastructure hydration
    internal Booking() { }

    public static Booking Create(
        Guid tenantId,
        Guid bookingTypeId,
        DateTimeOffset start,
        DateTimeOffset end,
        string customerId,
        string customerEmail,
        string? paymentReference = null)
    {
        return new Booking
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Start = start,
            End = end,
            Status = BookingStatus.PendingPayment,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            PaymentReference = paymentReference
        };
    }

    public void Pay(string changedById, string changedByRole)
    {
        if (Status != BookingStatus.PendingPayment)
            throw new InvalidStateTransitionException(Status, "pay");
        Transition(BookingStatus.PendingVerification, changedById, changedByRole);
    }

    public void Confirm(string changedById, string changedByRole)
    {
        if (Status != BookingStatus.PendingVerification)
            throw new InvalidStateTransitionException(Status, "confirm");
        Transition(BookingStatus.Confirmed, changedById, changedByRole);
    }

    public void Cancel(string changedById, string changedByRole)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidStateTransitionException(Status, "cancel");
        Transition(BookingStatus.Cancelled, changedById, changedByRole);
    }

    public void SoftDelete() => IsDeleted = true;

    private void Transition(BookingStatus to, string changedById, string changedByRole)
    {
        var change = new BookingStatusChange(Id, Status, to, changedById, changedByRole);
        _statusChanges.Add(change);
        Status = to;
    }
}
