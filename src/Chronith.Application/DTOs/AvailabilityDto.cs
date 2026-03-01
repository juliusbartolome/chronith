namespace Chronith.Application.DTOs;

public sealed record AvailabilityDto(
    IReadOnlyList<AvailableSlotDto> Slots
);

public sealed record AvailableSlotDto(
    DateTimeOffset Start,
    DateTimeOffset End
);
