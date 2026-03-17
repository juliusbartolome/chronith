namespace Chronith.Domain.Models;

public sealed class TimeBlock
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? BookingTypeId { get; private set; }
    public Guid? StaffMemberId { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public string? Reason { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal TimeBlock() { }

    public static TimeBlock Create(
        Guid tenantId, Guid? bookingTypeId, Guid? staffMemberId,
        DateTimeOffset start, DateTimeOffset end, string? reason)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            StaffMemberId = staffMemberId,
            Start = start,
            End = end,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow
        };

    public void SoftDelete() => IsDeleted = true;
}
