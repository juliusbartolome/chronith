using Microsoft.AspNetCore.Authentication;

namespace Chronith.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeLabel = "ApiKey";
}
