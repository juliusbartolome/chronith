using System.Security.Claims;
using System.Text.Encodings.Web;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class ApiKeyAuthenticationHandlerTests
{
    // Helper to build an initialized handler
    private static async Task<ApiKeyAuthenticationHandler> BuildHandlerAsync(
        IApiKeyRepository apiKeyRepo,
        string? headerValue = null)
    {
        var options = new ApiKeyAuthenticationOptions();
        var optionsMonitor = Substitute.For<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        optionsMonitor.Get(Arg.Any<string>()).Returns(options);
        optionsMonitor.CurrentValue.Returns(options);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var encoder = UrlEncoder.Default;

        // Build a scope factory whose scope resolves the provided repo
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IApiKeyRepository)).Returns(apiKeyRepo);
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        var handler = new ApiKeyAuthenticationHandler(optionsMonitor, loggerFactory, encoder, apiKeyRepo, scopeFactory);

        var context = new DefaultHttpContext();
        if (headerValue is not null)
            context.Request.Headers["X-Api-Key"] = headerValue;

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationOptions.SchemeLabel,
            null,
            typeof(ApiKeyAuthenticationHandler));

        await ((IAuthenticationHandler)handler).InitializeAsync(scheme, context);
        return handler;
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoHeader_ReturnsNoResult()
    {
        // Arrange
        var repo = Substitute.For<IApiKeyRepository>();
        var handler = await BuildHandlerAsync(repo, headerValue: null);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.None.Should().BeTrue("no X-Api-Key header should yield NoResult");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKey_ReturnsSuccess_WithClaims()
    {
        // Arrange
        var (rawKey, hash) = TenantApiKey.GenerateKey();
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var apiKey = new TenantApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            KeyHash = hash,
            Description = "test",
            Scopes = [ApiKeyScope.BookingsRead],
        };

        var repo = Substitute.For<IApiKeyRepository>();
        repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);
        repo.UpdateLastUsedAtAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

        // Act
        var result = await handler.AuthenticateAsync();
        await Task.Delay(50); // allow fire-and-forget to complete

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        var claims = result.Principal!.Claims.ToList();
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        claims.Should().Contain(c => c.Type == "scope" && c.Value == ApiKeyScope.BookingsRead);
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ApiKey");
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == keyId.ToString());
        claims.Should().Contain(c => c.Type == "sub" && c.Value == keyId.ToString());

        await repo.Received(1).UpdateLastUsedAtAsync(keyId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKey_EmitsScopeClaimsAndSyntheticRole()
    {
        // Arrange
        var (rawKey, hash) = TenantApiKey.GenerateKey();
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var apiKey = new TenantApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            KeyHash = hash,
            Description = "test",
            Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
        };

        var repo = Substitute.For<IApiKeyRepository>();
        repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);
        repo.UpdateLastUsedAtAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

        // Act
        var result = await handler.AuthenticateAsync();
        await Task.Delay(50); // allow fire-and-forget to complete

        // Assert
        result.Succeeded.Should().BeTrue();
        var claims = result.Principal!.Claims.ToList();

        // Scope claims — one per scope
        claims.Should().Contain(c => c.Type == "scope" && c.Value == ApiKeyScope.BookingsRead);
        claims.Should().Contain(c => c.Type == "scope" && c.Value == ApiKeyScope.StaffRead);

        // Synthetic "ApiKey" role claim
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ApiKey");

        // No old free-form role
        claims.Should().NotContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");

        // Tenant and identity claims still present
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == keyId.ToString());
    }

    [Fact]
    public async Task HandleAuthenticateAsync_KeyWithNoScopes_EmitsSyntheticRoleOnly()
    {
        var (rawKey, hash) = TenantApiKey.GenerateKey();
        var apiKey = new TenantApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            KeyHash = hash,
            Description = "empty scopes",
            Scopes = [],
        };

        var repo = Substitute.For<IApiKeyRepository>();
        repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);
        repo.UpdateLastUsedAtAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);
        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        var claims = result.Principal!.Claims.ToList();
        claims.Should().NotContain(c => c.Type == "scope");
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ApiKey");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_RevokedOrUnknownKey_ReturnsFail()
    {
        // Arrange
        var (rawKey, _) = TenantApiKey.GenerateKey();

        var repo = Substitute.For<IApiKeyRepository>();
        // GetByHashAsync returns null for revoked/unknown keys
        repo.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(default(TenantApiKey));

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeFalse("a known-bad key should Fail, not NoResult");
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or revoked API key");
    }
}
