using System.Security.Claims;
using Chronith.Domain.Exceptions;

namespace Chronith.API.Endpoints.Public.CustomerAuth;

public static class CustomerClaimExtensions
{
    public static Guid GetCustomerIdFromClaims(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("customer_id")
            ?? throw new UnauthorizedException("Missing customer_id claim");

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new UnauthorizedException("Invalid customer_id claim");
    }
}
