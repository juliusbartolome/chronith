// src/Chronith.Application/Models/ApiKeyScope.cs
namespace Chronith.Application.Models;

public static class ApiKeyScope
{
    public const string BookingsRead    = "bookings:read";
    public const string BookingsWrite   = "bookings:write";
    public const string BookingsDelete  = "bookings:delete";
    public const string BookingsConfirm = "bookings:confirm";
    public const string BookingsCancel  = "bookings:cancel";
    public const string BookingsPay     = "bookings:pay";
    public const string AvailabilityRead  = "availability:read";
    public const string StaffRead         = "staff:read";
    public const string StaffWrite        = "staff:write";
    public const string BookingTypesRead  = "booking-types:read";
    public const string BookingTypesWrite = "booking-types:write";
    public const string AnalyticsRead     = "analytics:read";
    public const string WebhooksRead      = "webhooks:read";
    public const string WebhooksWrite     = "webhooks:write";
    public const string TenantRead        = "tenant:read";
    public const string TenantWrite       = "tenant:write";
    public const string AuditRead                 = "audit:read";
    public const string NotificationsWrite         = "notifications:write";
    public const string NotificationTemplatesWrite = "notification-templates:write";
    public const string TimeBlocksWrite            = "time-blocks:write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        BookingsRead, BookingsWrite, BookingsDelete, BookingsConfirm, BookingsCancel,
        BookingsPay, AvailabilityRead, StaffRead, StaffWrite, BookingTypesRead,
        BookingTypesWrite, AnalyticsRead, WebhooksRead, WebhooksWrite,
        TenantRead, TenantWrite, AuditRead, NotificationsWrite,
        NotificationTemplatesWrite, TimeBlocksWrite,
    };
}
