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
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Mobile { get; private set; }
    public Guid? CustomerAccountId { get; private set; }
    public string? PaymentReference { get; private set; }
    public long AmountInCentavos { get; private set; }
    public string Currency { get; private set; } = "PHP";
    public string? CheckoutUrl { get; private set; }
    public Guid? StaffMemberId { get; private set; }
    public string? CustomFields { get; private set; }
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
        long amountInCentavos,
        string currency,
        string? paymentReference = null,
        string? customFields = null,
        string? firstName = null,
        string? lastName = null,
        string? mobile = null)
    {
        var isFree = amountInCentavos == 0;
        return new Booking
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Start = start,
            End = end,
            Status = isFree ? BookingStatus.PendingVerification : BookingStatus.PendingPayment,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            AmountInCentavos = amountInCentavos,
            Currency = currency,
            PaymentReference = paymentReference,
            CustomFields = customFields,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            Mobile = mobile
        };
    }

    public void LinkCustomerAccount(Guid customerAccountId)
    {
        CustomerAccountId = customerAccountId;
    }

    public void SetCheckoutDetails(string checkoutUrl, string providerTransactionId)
    {
        CheckoutUrl = checkoutUrl;
        PaymentReference = providerTransactionId;
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

    public void AssignStaff(Guid staffMemberId, string changedById, string changedByRole)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidStateTransitionException(Status, "assign staff");
        StaffMemberId = staffMemberId;
    }

    public void UnassignStaff(string changedById, string changedByRole)
    {
        StaffMemberId = null;
    }

    public void Reschedule(DateTimeOffset newStart, DateTimeOffset newEnd, string changedById, string changedByRole)
    {
        if (Status == BookingStatus.Cancelled)
            throw new InvalidStateTransitionException(Status, "reschedule");
        Start = newStart;
        End = newEnd;
        // Record as a status change for audit trail (same status, captures the reschedule event)
        _statusChanges.Add(new BookingStatusChange(Id, Status, Status, changedById, changedByRole));
    }

    public void SoftDelete() => IsDeleted = true;

    public void SetPaymentReference(string? paymentReference)
        => PaymentReference = paymentReference;

    public void SetCheckoutUrl(string? checkoutUrl)
        => CheckoutUrl = checkoutUrl;

    private void Transition(BookingStatus to, string changedById, string changedByRole)
    {
        var change = new BookingStatusChange(Id, Status, to, changedById, changedByRole);
        _statusChanges.Add(change);
        Status = to;
    }
}
