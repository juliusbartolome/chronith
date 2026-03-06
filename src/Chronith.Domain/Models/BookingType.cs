namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;

public abstract class BookingType
{
    public Guid Id { get; protected set; }
    public Guid TenantId { get; protected set; }
    public string Slug { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public int Capacity { get; protected set; }
    public PaymentMode PaymentMode { get; protected set; }
    public string? PaymentProvider { get; protected set; }
    public long PriceInCentavos { get; protected set; }
    public string Currency { get; protected set; } = "PHP";
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// Given a requested UTC start time, validates it falls within an availability window
    /// and returns the UTC (start, end) pair for the booking.
    /// </summary>
    public abstract (DateTimeOffset Start, DateTimeOffset End) ResolveSlot(
        DateTimeOffset requestedStart, TenantTimeZone tz);

    /// <summary>
    /// Returns the effective conflict range for an existing booking, expanded by buffers.
    /// Used in conflict SQL: effectiveStart &lt; newEnd AND effectiveEnd &gt; newStart.
    /// </summary>
    public abstract (DateTimeOffset EffectiveStart, DateTimeOffset EffectiveEnd)
        GetEffectiveRange(DateTimeOffset start, DateTimeOffset end);

    /// <summary>Updates mutable fields. Subclasses extend this for type-specific fields.</summary>
    public virtual void Update(
        string name,
        int capacity,
        PaymentMode paymentMode,
        string? paymentProvider,
        int durationMinutes,
        int bufferBeforeMinutes,
        int bufferAfterMinutes,
        IReadOnlyList<TimeSlotWindow>? availabilityWindows,
        IReadOnlyList<DayOfWeek>? availableDays,
        long priceInCentavos,
        string currency)
    {
        Name = name;
        Capacity = capacity;
        PaymentMode = paymentMode;
        PaymentProvider = paymentProvider;
        PriceInCentavos = priceInCentavos;
        Currency = currency;
    }

    public void SoftDelete() => IsDeleted = true;
}
