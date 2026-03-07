namespace Chronith.Application.DTOs;

public sealed record TimeBlockDto(
    Guid Id,
    Guid? BookingTypeId,
    Guid? StaffMemberId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Reason,
    DateTimeOffset CreatedAt);
