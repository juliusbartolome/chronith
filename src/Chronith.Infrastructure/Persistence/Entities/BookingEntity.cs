using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class BookingEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingTypeId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public BookingStatus Status { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public long AmountInCentavos { get; set; }
    public string Currency { get; set; } = "PHP";
    public string? CheckoutUrl { get; set; }
    public Guid? StaffMemberId { get; set; }
    public string? CustomFields { get; set; }
    public Guid? CustomerAccountId { get; set; }
    public Guid? RecurrenceRuleId { get; set; }
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }

    // Navigation
    public BookingTypeEntity? BookingType { get; set; }
    public StaffMemberEntity? StaffMember { get; set; }
    public List<BookingStatusChangeEntity> StatusChanges { get; set; } = new();
}
