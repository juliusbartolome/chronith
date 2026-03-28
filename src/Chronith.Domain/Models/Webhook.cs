namespace Chronith.Domain.Models;

public sealed class Webhook
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BookingTypeId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }

    private readonly List<string> _eventTypes = [];
    public IReadOnlyList<string> EventTypes => _eventTypes.AsReadOnly();

    internal Webhook() { }

    public static Webhook Create(Guid tenantId, Guid bookingTypeId, string url, string secret,
        IReadOnlyList<string> eventTypes)
    {
        ValidateEventTypes(eventTypes);

        var webhook = new Webhook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Url = url,
            Secret = secret
        };
        webhook._eventTypes.AddRange(eventTypes.Distinct());
        return webhook;
    }

    public void UpdateSubscriptions(IReadOnlyList<string> eventTypes)
    {
        ValidateEventTypes(eventTypes);
        _eventTypes.Clear();
        _eventTypes.AddRange(eventTypes.Distinct());
    }

    public void Update(string? url, string? secret, IReadOnlyList<string>? eventTypes)
    {
        if (url is not null) Url = url;
        if (secret is not null) Secret = secret;
        if (eventTypes is not null) UpdateSubscriptions(eventTypes);
    }

    public void SoftDelete() => IsDeleted = true;

    private static void ValidateEventTypes(IReadOnlyList<string> eventTypes)
    {
        if (eventTypes.Count == 0)
            throw new ArgumentException("At least one event type is required.", nameof(eventTypes));

        var invalid = eventTypes.Where(e => !WebhookEventTypes.IsValid(e)).ToList();
        if (invalid.Count > 0)
            throw new ArgumentException(
                $"Unknown event type(s): {string.Join(", ", invalid)}", nameof(eventTypes));
    }
}
