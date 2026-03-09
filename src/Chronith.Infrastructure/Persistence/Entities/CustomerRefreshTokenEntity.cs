namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class CustomerRefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
}
