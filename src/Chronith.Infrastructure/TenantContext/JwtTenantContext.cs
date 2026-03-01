using Chronith.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Chronith.Infrastructure.TenantContext;

/// <summary>
/// Reads tenant_id, sub, and role from the current JWT bearer token.
/// Registered as Scoped — one instance per HTTP request.
/// </summary>
public sealed class JwtTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public JwtTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User
                .FindFirst("tenant_id")?.Value;
            return claim is not null && Guid.TryParse(claim, out var id)
                ? id
                : Guid.Empty;
        }
    }

    public string UserId
        => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
           ?? string.Empty;

    public string Role
        => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value
           ?? _httpContextAccessor.HttpContext?.User.FindFirst("role")?.Value
           ?? string.Empty;
}
