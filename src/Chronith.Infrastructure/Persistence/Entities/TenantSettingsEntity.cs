namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantSettingsEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#2563EB";
    public string? AccentColor { get; set; }
    public string? CustomDomain { get; set; }
    public bool BookingPageEnabled { get; set; } = true;
    public string? WelcomeMessage { get; set; }
    public string? TermsUrl { get; set; }
    public string? PrivacyUrl { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public uint Version { get; set; }
}
