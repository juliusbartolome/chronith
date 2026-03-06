namespace Chronith.Application.Notifications;

public sealed record BookingEventPayload(
    string Event,
    Guid BookingId,
    Guid TenantId,
    string BookingTypeSlug,
    string Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail,
    DateTimeOffset OccurredAt);
