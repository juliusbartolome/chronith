namespace Chronith.Client.Models;

public sealed record BookingTypeDto(
    Guid Id,
    Guid TenantId,
    string Title,
    string Slug,
    string Kind,
    string? Description,
    long PriceCentavos,
    int DurationMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt
);
