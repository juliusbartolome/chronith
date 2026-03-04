namespace Chronith.Application.Interfaces;

public interface IRateLimitStore
{
    /// <summary>Returns the per-window permit limit for the given tenant.</summary>
    int GetPermitLimit(string tenantId);
}
