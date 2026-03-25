using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record PublicBookingStatusDto(
    Guid Id,
    string ReferenceId,
    BookingStatus Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    long AmountInCentavos,
    string Currency,
    string? PaymentReference,
    string? CheckoutUrl
);
