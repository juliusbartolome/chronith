using System.Security.Claims;
using System.Text.Encodings.Web;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyRepository apiKeyRepo,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    private const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var rawKeyValues)
            || string.IsNullOrWhiteSpace(rawKeyValues))
            return AuthenticateResult.NoResult();

        var rawKey = rawKeyValues.ToString();
        var hash = TenantApiKey.ComputeHash(rawKey);
        var key = await apiKeyRepo.GetByHashAsync(hash, Context.RequestAborted);

        if (key is null)
            return AuthenticateResult.Fail("Invalid or revoked API key");

        // Fire-and-forget last-used update — uses a fresh DI scope so that the
        // scoped IApiKeyRepository is not captured after the request scope disposes.
        _ = UpdateLastUsedAtSafeAsync(scopeFactory, key.Id, Logger);

        var claims = new[]
        {
            new Claim("tenant_id", key.TenantId.ToString()),
            new Claim(ClaimTypes.Role, key.Role),
            new Claim(ClaimTypes.NameIdentifier, key.Id.ToString()),
            new Claim("sub", key.Id.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static async Task UpdateLastUsedAtSafeAsync(
        IServiceScopeFactory scopeFactory, Guid id, ILogger logger)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            await repo.UpdateLastUsedAtAsync(id, DateTimeOffset.UtcNow, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to update LastUsedAt for key {Id}", id);
        }
    }
}
