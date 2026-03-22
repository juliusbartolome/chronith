using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record PublicBookingStatusDto(
    Guid Id,
    BookingStatus Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    long AmountInCentavos,
    string Currency,
    string? PaymentReference,
    string? CheckoutUrl
);
