namespace Chronith.Application.DTOs;

public sealed record TenantMetricsDto(
    BookingMetricsDto Bookings,
    WebhookMetricsDto Webhooks,
    BookingTypeMetricsDto BookingTypes);

public sealed record BookingMetricsDto(
    int Total,
    Dictionary<string, int> ByStatus,
    int ThisMonth);

public sealed record WebhookMetricsDto(
    int TotalDeliveries,
    int Delivered,
    int Failed,
    decimal? DeliveryRatePct);

public sealed record BookingTypeMetricsDto(
    int Active,
    int Archived);
