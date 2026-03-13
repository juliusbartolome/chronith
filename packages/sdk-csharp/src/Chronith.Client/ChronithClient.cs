using Chronith.Client.Services;

namespace Chronith.Client;

/// <summary>
/// Top-level client for the Chronith booking engine API.
/// </summary>
public sealed class ChronithClient
{
    public BookingsService Bookings { get; }
    public BookingTypesService BookingTypes { get; }
    public StaffService Staff { get; }
    public AvailabilityService Availability { get; }
    public AnalyticsService Analytics { get; }
    public WebhooksService Webhooks { get; }
    public NotificationsService Notifications { get; }
    public RecurringService Recurring { get; }
    public AuditService Audit { get; }
    public TenantService Tenant { get; }

    public ChronithClient(HttpClient httpClient)
    {
        Bookings = new BookingsService(httpClient);
        BookingTypes = new BookingTypesService(httpClient);
        Staff = new StaffService(httpClient);
        Availability = new AvailabilityService(httpClient);
        Analytics = new AnalyticsService(httpClient);
        Webhooks = new WebhooksService(httpClient);
        Notifications = new NotificationsService(httpClient);
        Recurring = new RecurringService(httpClient);
        Audit = new AuditService(httpClient);
        Tenant = new TenantService(httpClient);
    }
}
