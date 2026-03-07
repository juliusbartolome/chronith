namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TimeBlockEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? BookingTypeId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Reason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
