using Chronith.Domain.Enums;

namespace Chronith.Infrastructure.Persistence.Entities;

/// <summary>
/// Single table for both TimeSlot and Calendar booking types.
/// Kind discriminator determines which fields are relevant.
/// </summary>
public sealed class BookingTypeEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public string? PaymentProvider { get; set; }
    public long PriceInCentavos { get; set; }
    public string Currency { get; set; } = "PHP";
    public BookingKind Kind { get; set; }
    public bool IsDeleted { get; set; }
    public bool RequiresStaffAssignment { get; set; }
    public string? CustomFieldSchema { get; set; }
    public string? ReminderIntervals { get; set; }

    // TimeSlot-specific (null for Calendar)
    public int? DurationMinutes { get; set; }
    public int? BufferBeforeMinutes { get; set; }
    public int? BufferAfterMinutes { get; set; }

    // Calendar-specific (null for TimeSlot) — stored as comma-separated int values
    public string? AvailableDays { get; set; }

    // Optimistic concurrency
    public uint RowVersion { get; set; }

    // Customer callback
    public string? CustomerCallbackUrl { get; set; }
    public string? CustomerCallbackSecret { get; set; }

    // Navigation
    public List<AvailabilityWindowEntity> AvailabilityWindows { get; set; } = new();
    public List<BookingEntity> Bookings { get; set; } = new();
    public List<WebhookEntity> Webhooks { get; set; } = new();
    public List<BookingTypeStaffAssignmentEntity> StaffAssignments { get; set; } = new();
}
