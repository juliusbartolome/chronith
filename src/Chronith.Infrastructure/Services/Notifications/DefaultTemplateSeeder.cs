using Chronith.Application.Interfaces;
using Chronith.Domain.Models;

namespace Chronith.Infrastructure.Services.Notifications;

public sealed class DefaultTemplateSeeder(
    INotificationTemplateRepository templateRepo,
    IUnitOfWork unitOfWork) : IDefaultTemplateSeeder
{
    private static readonly string[] EventTypes =
    [
        "booking.confirmed",
        "booking.cancelled",
        "booking.rescheduled",
        "waitlist.offered",
        "reminder",
        "customer.welcome"
    ];

    private static readonly string[] ChannelTypes = ["email", "sms", "push"];

    public async Task SeedForEventTypeAsync(
        Guid tenantId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        var templatesToAdd = new List<NotificationTemplate>();

        foreach (var channelType in ChannelTypes)
        {
            var existing = await templateRepo.GetByEventAndChannelAsync(
                tenantId, eventType, channelType, cancellationToken);

            if (existing is not null) continue;

            var (subject, body) = GetDefaultContent(eventType, channelType);
            var template = NotificationTemplate.Create(tenantId, eventType, channelType, subject, body);
            templatesToAdd.Add(template);
        }

        if (templatesToAdd.Count == 0) return;

        await templateRepo.AddRangeAsync(templatesToAdd, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var eventType in EventTypes)
        {
            await SeedForEventTypeAsync(tenantId, eventType, cancellationToken);
        }
    }

    private static (string? subject, string body) GetDefaultContent(string eventType, string channelType)
    {
        return eventType switch
        {
            "booking.confirmed" => channelType switch
            {
                "email" => (
                    "Your booking is confirmed — {{booking_type_slug}}",
                    "Hi {{customer_name}},\n\nYour booking for {{booking_type_slug}} on {{booking_date}} at {{booking_time}} has been confirmed.\n\nBooking reference: {{booking_id}}\n\nThank you for choosing us!"),
                "sms" => (
                    null,
                    "Booking confirmed: {{booking_type_slug}} on {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}"),
                _ => (
                    null,
                    "Your booking for {{booking_type_slug}} on {{booking_date}} at {{booking_time}} is confirmed. Ref: {{booking_id}}")
            },
            "booking.cancelled" => channelType switch
            {
                "email" => (
                    "Your booking has been cancelled — {{booking_type_slug}}",
                    "Hi {{customer_name}},\n\nYour booking for {{booking_type_slug}} on {{booking_date}} at {{booking_time}} has been cancelled.\n\nBooking reference: {{booking_id}}\n\nIf you have any questions, please contact us."),
                "sms" => (
                    null,
                    "Booking cancelled: {{booking_type_slug}} on {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}"),
                _ => (
                    null,
                    "Your booking for {{booking_type_slug}} on {{booking_date}} at {{booking_time}} has been cancelled. Ref: {{booking_id}}")
            },
            "booking.rescheduled" => channelType switch
            {
                "email" => (
                    "Your booking has been rescheduled — {{booking_type_slug}}",
                    "Hi {{customer_name}},\n\nYour booking for {{booking_type_slug}} has been rescheduled to {{booking_date}} at {{booking_time}}.\n\nBooking reference: {{booking_id}}"),
                "sms" => (
                    null,
                    "Booking rescheduled: {{booking_type_slug}} now on {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}"),
                _ => (
                    null,
                    "Your booking for {{booking_type_slug}} has been rescheduled to {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}")
            },
            "waitlist.offered" => channelType switch
            {
                "email" => (
                    "A spot is available — {{booking_type_slug}}",
                    "Hi {{customer_name}},\n\nGreat news! A spot has opened up for {{booking_type_slug}} on {{booking_date}} at {{booking_time}}.\n\nPlease confirm your booking within {{expiry_hours}} hours to secure your spot."),
                "sms" => (
                    null,
                    "Spot available for {{booking_type_slug}} on {{booking_date}} at {{booking_time}}. Confirm within {{expiry_hours}} hrs."),
                _ => (
                    null,
                    "A spot is available for {{booking_type_slug}} on {{booking_date}} at {{booking_time}}. Confirm within {{expiry_hours}} hours.")
            },
            "reminder" => channelType switch
            {
                "email" => (
                    "Reminder: upcoming booking — {{booking_type_slug}}",
                    "Hi {{customer_name}},\n\nThis is a reminder for your upcoming booking:\n\n{{booking_type_slug}} on {{booking_date}} at {{booking_time}}\n\nBooking reference: {{booking_id}}"),
                "sms" => (
                    null,
                    "Reminder: {{booking_type_slug}} on {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}"),
                _ => (
                    null,
                    "Reminder: your booking for {{booking_type_slug}} is on {{booking_date}} at {{booking_time}}. Ref: {{booking_id}}")
            },
            "customer.welcome" => channelType switch
            {
                "email" => (
                    "Welcome to {{tenant_name}}!",
                    "Hi {{customer_name}},\n\nWelcome to {{tenant_name}}! Your account has been created successfully.\n\nYou can now book appointments online at your convenience."),
                "sms" => (
                    null,
                    "Welcome to {{tenant_name}}, {{customer_name}}! Your account is ready. Book appointments online anytime."),
                _ => (
                    null,
                    "Welcome to {{tenant_name}}, {{customer_name}}! Your account has been created.")
            },
            _ => channelType switch
            {
                "email" => (
                    "Notification from {{tenant_name}}",
                    "Hi {{customer_name}},\n\nYou have a new notification regarding your booking {{booking_id}}."),
                "sms" => (
                    null,
                    "Notification from {{tenant_name}}: booking {{booking_id}} update."),
                _ => (
                    null,
                    "You have a new notification regarding your booking {{booking_id}}.")
            }
        };
    }
}
