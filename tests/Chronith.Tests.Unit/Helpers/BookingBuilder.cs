using Chronith.Domain.Enums;
using Chronith.Domain.Models;

namespace Chronith.Tests.Unit.Helpers;

/// <summary>
/// Builder that creates a Booking in various states for test convenience.
/// Uses Booking.Create(...) then calls transitions to reach the desired state.
/// </summary>
public sealed class BookingBuilder
{
    private Guid _tenantId = Guid.NewGuid();
    private Guid _bookingTypeId = Guid.NewGuid();
    private DateTimeOffset _start = DateTimeOffset.UtcNow;
    private DateTimeOffset _end = DateTimeOffset.UtcNow.AddHours(1);
    private string _customerId = "customer-1";
    private string _customerEmail = "customer@example.com";
    private string? _paymentReference = null;
    private long _amountInCentavos = 0;
    private string _currency = "PHP";
    private BookingStatus _targetStatus = BookingStatus.PendingPayment;

    public BookingBuilder WithTenantId(Guid tenantId) { _tenantId = tenantId; return this; }
    public BookingBuilder WithBookingTypeId(Guid id) { _bookingTypeId = id; return this; }
    public BookingBuilder WithStart(DateTimeOffset start) { _start = start; return this; }
    public BookingBuilder WithEnd(DateTimeOffset end) { _end = end; return this; }
    public BookingBuilder WithCustomerId(string id) { _customerId = id; return this; }
    public BookingBuilder WithCustomerEmail(string email) { _customerEmail = email; return this; }
    public BookingBuilder WithPaymentReference(string? reference) { _paymentReference = reference; return this; }
    public BookingBuilder WithAmountInCentavos(long amount) { _amountInCentavos = amount; return this; }
    public BookingBuilder WithCurrency(string currency) { _currency = currency; return this; }

    public BookingBuilder InStatus(BookingStatus status)
    {
        _targetStatus = status;
        // Ensure amount matches expected starting state
        if (status == BookingStatus.PendingPayment ||
            status == BookingStatus.PendingVerification ||
            status == BookingStatus.Confirmed)
        {
            _amountInCentavos = 10000; // nonzero so booking starts in PendingPayment
        }
        return this;
    }

    public Booking Build()
    {
        var booking = Booking.Create(
            _tenantId,
            _bookingTypeId,
            _start,
            _end,
            _customerId,
            _customerEmail,
            _amountInCentavos,
            _currency,
            _paymentReference);

        const string actor = "test-user";
        const string role = "test-role";

        switch (_targetStatus)
        {
            case BookingStatus.PendingPayment:
                // already in this state (amount > 0 ensured by InStatus)
                break;
            case BookingStatus.PendingVerification:
                booking.Pay(actor, role);
                break;
            case BookingStatus.Confirmed:
                booking.Pay(actor, role);
                booking.Confirm(actor, role);
                break;
            case BookingStatus.Cancelled:
                booking.Cancel(actor, role);
                break;
            default:
                throw new InvalidOperationException($"Unknown status: {_targetStatus}");
        }

        return booking;
    }
}
