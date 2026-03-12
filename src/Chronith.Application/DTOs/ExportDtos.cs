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
/// Only Date and TotalBookings are exported — ByStatus aggregates cover the whole period
/// and repeating them on every row would be misleading.
/// </summary>
public sealed record AnalyticsExportRowDto(
    string Date,
    int TotalBookings);

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
/// RowCount reflects the number of data rows; if it equals 10,000 the result was truncated.
/// </summary>
public sealed record ExportFileResult(
    byte[] Content,
    string ContentType,
    string FileName,
    int RowCount = 0);
