using Microsoft.AspNetCore.Authorization;

namespace Chronith.API.Authorization;

/// <summary>
/// Authorization requirement: if the caller authenticated via the ApiKey scheme,
/// they must have the specified scope claim. JWT callers always satisfy this requirement.
/// </summary>
public sealed class ApiKeyScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
