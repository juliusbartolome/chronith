namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class IdempotencyKeyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string EndpointRoute { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public string ResponseContentType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
