namespace Chronith.Domain.Models;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;

public sealed class WaitlistEntry
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BookingTypeId { get; private set; }
    public Guid? StaffMemberId { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public DateTimeOffset DesiredStart { get; private set; }
    public DateTimeOffset DesiredEnd { get; private set; }
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset? OfferedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    internal WaitlistEntry() { }

    public static WaitlistEntry Create(
        Guid tenantId, Guid bookingTypeId, Guid? staffMemberId,
        string customerId, string customerEmail,
        DateTimeOffset desiredStart, DateTimeOffset desiredEnd)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            StaffMemberId = staffMemberId,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            DesiredStart = desiredStart,
            DesiredEnd = desiredEnd,
            Status = WaitlistStatus.Waiting,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public void Offer(DateTimeOffset now, TimeSpan offerTtl)
    {
        if (Status != WaitlistStatus.Waiting)
            throw new InvalidStateTransitionException($"Cannot perform 'Offer' on a waitlist entry in '{Status}' status.");
        Status = WaitlistStatus.Offered;
        OfferedAt = now;
        ExpiresAt = now.Add(offerTtl);
    }

    public void Accept()
    {
        if (Status != WaitlistStatus.Offered)
            throw new InvalidStateTransitionException($"Cannot perform 'Accept' on a waitlist entry in '{Status}' status.");
        Status = WaitlistStatus.Converted;
    }

    public void Expire()
    {
        if (Status != WaitlistStatus.Offered)
            throw new InvalidStateTransitionException($"Cannot perform 'Expire' on a waitlist entry in '{Status}' status.");
        Status = WaitlistStatus.Expired;
    }

    public void SoftDelete() => IsDeleted = true;
}
