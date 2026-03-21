using Chronith.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Chronith.API.Authorization;

public sealed class ApiKeyScopeHandler : AuthorizationHandler<ApiKeyScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyScopeRequirement requirement)
    {
        // If the caller did NOT authenticate via the ApiKey scheme, bypass the scope check.
        // JWT callers use role-based auth — they always satisfy scope requirements.
        var isApiKeyCaller = context.User.Identities
            .Any(i => i.AuthenticationType == ApiKeyAuthenticationOptions.SchemeLabel);

        if (!isApiKeyCaller)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // API key caller: must possess the required scope claim
        if (context.User.HasClaim("scope", requirement.Scope))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
