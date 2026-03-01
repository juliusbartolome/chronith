using Chronith.Domain.Enums;
using MediatR;

namespace Chronith.Application.Notifications;

/// <summary>
/// Published after every booking status transition.
/// Handled by WebhookDeliveryHandler (stubbed in v0.1).
/// </summary>
public sealed record BookingStatusChangedNotification(
    Guid BookingId,
    BookingStatus? FromStatus,
    BookingStatus ToStatus) : INotification;
