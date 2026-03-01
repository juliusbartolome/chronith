namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class WebhookEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingTypeId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    // Navigation
    public BookingTypeEntity? BookingType { get; set; }
}
