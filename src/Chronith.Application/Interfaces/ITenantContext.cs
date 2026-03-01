using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

/// <summary>
/// Carries the resolved tenant identity from the current request context (e.g. JWT).
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string UserId { get; }
    string Role { get; }
}
