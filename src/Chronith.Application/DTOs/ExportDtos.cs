namespace Chronith.Application.DTOs;

/// <summary>
/// Flat projection used for bookings CSV/PDF export.
/// </summary>
public sealed record BookingExportRowDto(
    Guid Id,
    string BookingTypeName,
    string BookingTypeSlug,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Status,
    string CustomerEmail,
    string CustomerId,
    string? StaffMemberName,
    long AmountInCentavos,
    string Currency,
    string? PaymentReference);

/// <summary>
/// One row per time-series point from the booking analytics query.
/// </summary>
public sealed record AnalyticsExportRowDto(
    string Date,
    int TotalBookings,
    int PendingPayment,
    int PendingVerification,
    int Confirmed,
    int Cancelled);

/// <summary>
/// Flat projection of AuditEntryDto for CSV export.
/// </summary>
public sealed record AuditExportRowDto(
    Guid Id,
    DateTimeOffset Timestamp,
    string Action,
    string EntityType,
    Guid EntityId,
    string UserId,
    string UserRole);

/// <summary>
/// Result returned by export query handlers — a file ready to stream.
/// </summary>
public sealed record ExportFileResult(
    byte[] Content,
    string ContentType,
    string FileName);
