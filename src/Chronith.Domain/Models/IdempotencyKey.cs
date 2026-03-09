namespace Chronith.Domain.Models;

public sealed class IdempotencyKey
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string EndpointRoute { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    internal IdempotencyKey() { }

    public static IdempotencyKey Create(Guid tenantId, string key, string endpointRoute,
        string requestHash, int responseStatusCode, string responseBody, TimeSpan ttl)
    {
        var now = DateTimeOffset.UtcNow;
        return new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            EndpointRoute = endpointRoute,
            RequestHash = requestHash,
            ResponseStatusCode = responseStatusCode,
            ResponseBody = responseBody,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl)
        };
    }

    internal static IdempotencyKey Hydrate(
        Guid id, Guid tenantId, string key, string endpointRoute,
        string requestHash, int responseStatusCode, string responseBody,
        DateTimeOffset createdAt, DateTimeOffset expiresAt) => new()
    {
        Id = id,
        TenantId = tenantId,
        Key = key,
        EndpointRoute = endpointRoute,
        RequestHash = requestHash,
        ResponseStatusCode = responseStatusCode,
        ResponseBody = responseBody,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt
    };

    public bool MatchesRequest(string requestHash) => RequestHash == requestHash;

    public bool IsExpired() => ExpiresAt <= DateTimeOffset.UtcNow;
}
