namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantPaymentConfigEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string Settings { get; set; } = "{}";
    public string? PublicNote { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
