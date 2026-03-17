namespace Chronith.Client.Models;

public sealed record AvailabilityDto(
    IReadOnlyList<AvailableSlotDto> Slots
);

public sealed record AvailableSlotDto(
    DateTimeOffset Start,
    DateTimeOffset End
);
