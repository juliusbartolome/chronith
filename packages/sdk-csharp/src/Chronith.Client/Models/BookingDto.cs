namespace Chronith.Client.Models;

public sealed record BookingDto(
    Guid Id,
    Guid TenantId,
    Guid BookingTypeId,
    string BookingTypeTitle,
    Guid? StaffMemberId,
    string? StaffMemberName,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string Status,
    long PriceCentavos,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
