namespace Chronith.Client.Models;

public sealed record AvailabilityDto(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsAvailable,
    Guid? StaffMemberId,
    string? StaffMemberName
);
