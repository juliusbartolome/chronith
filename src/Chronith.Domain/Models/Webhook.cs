namespace Chronith.Domain.Models;

public sealed class Webhook
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BookingTypeId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }

    internal Webhook() { }

    public static Webhook Create(Guid tenantId, Guid bookingTypeId, string url, string secret)
        => new Webhook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingTypeId = bookingTypeId,
            Url = url,
            Secret = secret
        };

    public void SoftDelete() => IsDeleted = true;
}
