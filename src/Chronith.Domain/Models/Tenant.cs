namespace Chronith.Domain.Models;

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // For Infrastructure hydration
    internal Tenant() { }

    public static Tenant Create(string slug, string name, string timeZoneId)
    {
        // Validate timezone id is valid IANA
        TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); // throws if invalid
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            TimeZoneId = timeZoneId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public TenantTimeZone GetTimeZone() => new(TimeZoneId);
}
