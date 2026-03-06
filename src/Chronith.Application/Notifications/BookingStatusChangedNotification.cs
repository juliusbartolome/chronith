using Chronith.Domain.Enums;
using MediatR;

namespace Chronith.Application.Notifications;

/// <summary>
/// Published after every booking status transition.
/// Handled by WebhookOutboxHandler (writes to outbox; replaces v0.1 stub).
/// </summary>
public sealed record BookingStatusChangedNotification(
    Guid BookingId,
    Guid TenantId,
    Guid BookingTypeId,
    string BookingTypeSlug,
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail) : INotification;
