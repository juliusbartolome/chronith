namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class WebhookEventSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public string EventName { get; set; } = string.Empty;
}
