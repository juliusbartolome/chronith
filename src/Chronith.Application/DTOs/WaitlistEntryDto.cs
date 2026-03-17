using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record WaitlistEntryDto(
    Guid Id,
    Guid BookingTypeId,
    Guid? StaffMemberId,
    string CustomerId,
    string CustomerEmail,
    DateTimeOffset DesiredStart,
    DateTimeOffset DesiredEnd,
    WaitlistStatus Status,
    DateTimeOffset? OfferedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
