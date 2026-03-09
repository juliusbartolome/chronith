namespace Chronith.Domain.Models;

public sealed class CustomerRefreshToken
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal CustomerRefreshToken() { }

    public static CustomerRefreshToken Create(Guid customerId, string tokenHash, TimeSpan ttl)
    {
        return new CustomerRefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    internal static CustomerRefreshToken Hydrate(Guid id, Guid customerId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset? usedAt, DateTimeOffset createdAt) => new()
    {
        Id = id, CustomerId = customerId, TokenHash = tokenHash,
        ExpiresAt = expiresAt, UsedAt = usedAt, CreatedAt = createdAt
    };

    public bool IsValid() => UsedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
