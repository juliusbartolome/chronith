using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record BookingDto(
    Guid Id,
    Guid BookingTypeId,
    DateTimeOffset Start,
    DateTimeOffset End,
    BookingStatus Status,
    string CustomerId,
    string CustomerEmail,
    string? PaymentReference,
    long AmountInCentavos,
    string Currency,
    string? CheckoutUrl,
    IReadOnlyList<BookingStatusChangeDto> StatusChanges
);

public sealed record BookingStatusChangeDto(
    Guid Id,
    BookingStatus FromStatus,
    BookingStatus ToStatus,
    string ChangedById,
    string ChangedByRole,
    DateTimeOffset ChangedAt
);
