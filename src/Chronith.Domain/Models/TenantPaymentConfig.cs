namespace Chronith.Domain.Models;

public sealed class TenantPaymentConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public string Settings { get; private set; } = "{}";
    public string? PublicNote { get; private set; }
    public string? QrCodeUrl { get; private set; }
    public string? PaymentSuccessUrl { get; private set; }
    public string? PaymentFailureUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal TenantPaymentConfig() { }

    public static TenantPaymentConfig Create(
        Guid tenantId,
        string providerName,
        string label,
        string settings,
        string? publicNote,
        string? qrCodeUrl,
        string? paymentSuccessUrl = null,
        string? paymentFailureUrl = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderName = providerName,
            Label = label,
            IsActive = providerName.Equals("Manual", StringComparison.OrdinalIgnoreCase),
            IsDeleted = false,
            Settings = settings,
            PublicNote = publicNote,
            QrCodeUrl = qrCodeUrl,
            PaymentSuccessUrl = paymentSuccessUrl,
            PaymentFailureUrl = paymentFailureUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public void UpdateDetails(string label, string settings, string? publicNote, string? qrCodeUrl,
        string? paymentSuccessUrl = null, string? paymentFailureUrl = null)
    {
        Label = label;
        Settings = settings;
        PublicNote = publicNote;
        QrCodeUrl = qrCodeUrl;
        PaymentSuccessUrl = paymentSuccessUrl;
        PaymentFailureUrl = paymentFailureUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
