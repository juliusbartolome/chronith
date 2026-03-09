namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class RecurrenceRuleEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingTypeId { get; set; }
    public string Frequency { get; set; } = string.Empty;
    public int Interval { get; set; }
    public int[]? DaysOfWeek { get; set; }
    public DateOnly SeriesStart { get; set; }
    public DateOnly? SeriesEnd { get; set; }
    public int? MaxOccurrences { get; set; }
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
