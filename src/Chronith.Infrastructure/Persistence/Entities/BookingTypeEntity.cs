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
    public BookingKind Kind { get; set; }
    public bool IsDeleted { get; set; }

    // TimeSlot-specific (null for Calendar)
    public int? DurationMinutes { get; set; }
    public int? BufferBeforeMinutes { get; set; }
    public int? BufferAfterMinutes { get; set; }

    // Calendar-specific (null for TimeSlot) — stored as comma-separated int values
    public string? AvailableDays { get; set; }

    // Optimistic concurrency
    public uint RowVersion { get; set; }

    // Navigation
    public List<AvailabilityWindowEntity> AvailabilityWindows { get; set; } = new();
    public List<BookingEntity> Bookings { get; set; } = new();
    public List<WebhookEntity> Webhooks { get; set; } = new();
}
