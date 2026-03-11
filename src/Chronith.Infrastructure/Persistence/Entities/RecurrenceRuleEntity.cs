namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class RecurrenceRuleEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingTypeId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int Interval { get; set; }
    public int[]? DaysOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public DateOnly SeriesStart { get; set; }
    public DateOnly? SeriesEnd { get; set; }
    public int? MaxOccurrences { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
