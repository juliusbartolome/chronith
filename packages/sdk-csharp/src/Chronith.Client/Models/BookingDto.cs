namespace Chronith.Client.Models;

public sealed record BookingDto(
    Guid Id,
    Guid BookingTypeId,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Status,
    string CustomerId,
    string CustomerEmail,
    string? PaymentReference,
    long AmountInCentavos,
    string Currency,
    string? CheckoutUrl,
    Guid? StaffMemberId,
    IReadOnlyList<BookingStatusChangeDto> StatusChanges
);

public sealed record BookingStatusChangeDto(
    Guid Id,
    string FromStatus,
    string ToStatus,
    string ChangedById,
    string ChangedByRole,
    DateTimeOffset ChangedAt
);
